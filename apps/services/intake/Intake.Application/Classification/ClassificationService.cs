using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Intake.Application.Configuration;
using Intake.Contracts.Classification;
using Intake.Contracts.Configuration;
using Intake.Domain.Artifacts;
using Intake.Domain.Classification;
using Microsoft.Extensions.Logging;

namespace Intake.Application.Classification;

public sealed class ClassificationService(
    IClassificationRepository repository,
    IIntakeConfigurationService configurationService,
    ISynqAiProviderRegistry providerRegistry,
    IManagedAiPolicyDefaults managedPolicyDefaults,
    IIntakeArtifactContentReader contentReader,
    IClassificationAuditSink auditSink,
    ILogger<ClassificationService> logger) : IClassificationService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly System.Text.RegularExpressions.Regex SafeCode = new(
        "^[A-Z][A-Z0-9_]{2,63}$",
        System.Text.RegularExpressions.RegexOptions.Compiled);
    private static readonly System.Text.RegularExpressions.Regex CredentialReference = new(
        "^(secret|credential|connection)://[A-Za-z0-9._:/-]{1,250}$",
        System.Text.RegularExpressions.RegexOptions.Compiled | System.Text.RegularExpressions.RegexOptions.IgnoreCase);

    public async Task<TenantAiPolicyResponse?> GetPolicyAsync(
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var policy = await repository.FindPolicyAsync(tenantId, cancellationToken);
        return policy is null ? null : Map(policy);
    }

    public async Task<TenantAiPolicyResponse> UpsertPolicyAsync(
        Guid tenantId,
        UpsertTenantAiPolicyRequest request,
        Guid? actorId,
        string? correlationId,
        CancellationToken cancellationToken)
    {
        var accessMode = Normalize(request.AccessMode);
        var providerCode = Normalize(request.ProviderCode);
        var modelCode = request.ModelCode?.Trim() ?? string.Empty;
        var managedDefaults = managedPolicyDefaults.Current;
        if (accessMode is not SynqAiAccessModes.LegalSynqManaged and not SynqAiAccessModes.BringYourOwn)
            throw IntakeConfigurationException.BadRequest(
                "INVALID_AI_ACCESS_MODE",
                "AccessMode must be LEGALSynq_MANAGED or BYOAI.");
        if (accessMode == SynqAiAccessModes.LegalSynqManaged)
        {
            if ((providerCode.Length > 0 && providerCode != managedDefaults.ProviderCode) ||
                (modelCode.Length > 0 && modelCode != managedDefaults.ModelCode) ||
                (request.CredentialReference is not null &&
                 !string.Equals(
                     request.CredentialReference.Trim(),
                     managedDefaults.CredentialReference,
                     StringComparison.OrdinalIgnoreCase)))
                throw IntakeConfigurationException.BadRequest(
                    ClassificationFailureCodes.CredentialUnavailable,
                    "LegalSynq-managed policies use centrally approved provider, model, and credential references.");

            providerCode = managedDefaults.ProviderCode;
            modelCode = managedDefaults.ModelCode;
        }
        if (modelCode.Length is < 1 or > 128 ||
            modelCode.Any(character => char.IsWhiteSpace(character) || char.IsControl(character)))
            throw IntakeConfigurationException.BadRequest(
                "INVALID_AI_MODEL",
                "ModelCode must be between 1 and 128 non-whitespace characters.");
        if (request.MaxOutputTokens is < 1 or > 8192 ||
            request.TimeoutSeconds is < 1 or > 600 ||
            request.MaxAttempts is < 1 or > 10)
            throw IntakeConfigurationException.BadRequest(
                "INVALID_AI_GUARDRAILS",
                "AI output tokens, timeout, and retry limits are outside the supported bounds.");
        if (!providerRegistry.AvailableProviderCodes.Contains(providerCode, StringComparer.Ordinal))
            throw IntakeConfigurationException.BadRequest(
                ClassificationFailureCodes.ProviderUnavailable,
                $"AI provider '{providerCode}' is not configured or supported.");

        var credentialReference = accessMode == SynqAiAccessModes.LegalSynqManaged
            ? managedDefaults.CredentialReference
            : request.CredentialReference?.Trim();
        if (accessMode == SynqAiAccessModes.BringYourOwn &&
            (credentialReference is null ||
             !CredentialReference.IsMatch(credentialReference) ||
             !IsTenantCredentialReference(credentialReference, tenantId)))
        {
            throw IntakeConfigurationException.BadRequest(
                ClassificationFailureCodes.CredentialUnavailable,
                "BYOAI policies require a tenant-scoped credential reference; raw or shared credentials are not accepted.");
        }
        if (credentialReference is not null && !CredentialReference.IsMatch(credentialReference))
            throw IntakeConfigurationException.BadRequest(
                ClassificationFailureCodes.CredentialUnavailable,
                "CredentialReference must be a secret://, credential://, or connection:// reference.");
        if (accessMode == SynqAiAccessModes.LegalSynqManaged &&
            credentialReference is not null &&
            !IsPlatformCredentialReference(credentialReference))
            throw IntakeConfigurationException.BadRequest(
                ClassificationFailureCodes.CredentialUnavailable,
                "LegalSynq-managed policies may only use centrally managed platform credential references.");

        var existing = await repository.FindPolicyAsync(tenantId, cancellationToken);
        if (existing is not null &&
            request.PolicyVersion.HasValue &&
            request.PolicyVersion.Value != existing.PolicyVersion)
            throw IntakeConfigurationException.Conflict(
                ClassificationFailureCodes.ConcurrencyConflict,
                "The AI policy has changed since it was read.");

        var now = DateTimeOffset.UtcNow;
        var policy = existing ?? new TenantAiPolicy
        {
            Id = Guid.CreateVersion7(),
            TenantId = tenantId,
            CreatedAt = now,
            CreatedBy = actorId,
        };
        policy.IsEnabled = request.IsEnabled;
        policy.AccessMode = accessMode;
        policy.ProviderCode = providerCode;
        policy.ModelCode = modelCode;
        policy.CredentialReference = credentialReference;
        policy.MaxOutputTokens = request.MaxOutputTokens;
        policy.TimeoutSeconds = request.TimeoutSeconds;
        policy.MaxAttempts = request.MaxAttempts;
        policy.PolicyVersion = existing is null ? 1 : existing.PolicyVersion + 1;
        policy.UpdatedAt = now;
        policy.UpdatedBy = actorId;
        await repository.SavePolicyAsync(policy, cancellationToken);
        logger.LogInformation(
            "AI policy updated. Tenant={TenantId} Provider={ProviderCode} Model={ModelCode} AccessMode={AccessMode} PolicyVersion={Version} CorrelationId={CorrelationId}",
            tenantId, providerCode, modelCode, accessMode, policy.PolicyVersion, correlationId);
        return Map(policy);
    }

    public async Task<IReadOnlyList<ClassificationProfileResponse>> ListProfilesAsync(
        CancellationToken cancellationToken)
    {
        var profiles = await repository.ListProfilesAsync(cancellationToken);
        return profiles
            .OrderBy(profile => profile.Code)
            .ThenByDescending(profile => profile.Version)
            .Select(profile => new ClassificationProfileResponse(
                profile.Code,
                profile.DisplayName,
                profile.Description,
                profile.Version,
                profile.TaxonomyCode,
                profile.TaxonomyVersion,
                profile.PromptCode,
                profile.PromptVersion,
                profile.OutputSchemaVersion,
                profile.IsActive,
                profile.IsSystemDefined))
            .ToArray();
    }

    public async Task<ArtifactClassificationResponse> ClassifyAsync(
        Guid tenantId,
        Guid artifactId,
        string? processingProfileCode,
        Guid? actorId,
        string? correlationId,
        bool retry,
        CancellationToken cancellationToken)
    {
        var artifact = await repository.FindArtifactAsync(tenantId, artifactId, cancellationToken)
            ?? throw IntakeConfigurationException.NotFound(
                "ARTIFACT_NOT_FOUND",
                "The Intake artifact was not found for the current tenant.");
        var policy = await repository.FindPolicyAsync(tenantId, cancellationToken)
            ?? throw IntakeConfigurationException.BadRequest(
                ClassificationFailureCodes.PolicyMissing,
                "No AI policy is configured for this tenant.");
        if (!policy.IsEnabled)
            throw IntakeConfigurationException.BadRequest(
                ClassificationFailureCodes.PolicyDisabled,
                "AI classification is disabled for this tenant.");

        var resolved = await configurationService.ResolveAsync(
            tenantId,
            processingProfileCode,
            cancellationToken);
        if (!resolved.EffectiveConfiguration.EnableClassification)
            throw IntakeConfigurationException.BadRequest(
                "CLASSIFICATION_DISABLED",
                "Classification is disabled by the tenant processing profile.");

        var profileCode = Normalize(resolved.EffectiveConfiguration.ClassificationProfileCode);
        var profile = await repository.FindProfileAsync(profileCode, null, cancellationToken)
            ?? throw IntakeConfigurationException.NotFound(
                ClassificationFailureCodes.ProfileMissing,
                $"Classification profile '{profileCode}' is not available.");
        var taxonomy = await repository.FindTaxonomyAsync(
                profile.TaxonomyCode,
                profile.TaxonomyVersion,
                cancellationToken)
            ?? throw IntakeConfigurationException.NotFound(
                ClassificationFailureCodes.TaxonomyInvalid,
                "The classification taxonomy referenced by the profile is not available.");
        var prompt = await repository.FindPromptAsync(
                profile.PromptCode,
                profile.PromptVersion,
                cancellationToken)
            ?? throw IntakeConfigurationException.NotFound(
                ClassificationFailureCodes.PromptInvalid,
                "The classification prompt referenced by the profile is not available.");
        var taxonomyClasses = ClassificationTaxonomy.Parse(taxonomy);
        if (string.IsNullOrWhiteSpace(artifact.Sha256))
            throw IntakeConfigurationException.BadRequest(
                ClassificationFailureCodes.ArtifactHashMissing,
                "Classification requires a SHA-256-bound artifact.");
        if (!string.Equals(
                artifact.ProcessingStatus,
                IntakeArtifactProcessingStatuses.Completed,
                StringComparison.Ordinal))
            throw IntakeConfigurationException.BadRequest(
                ClassificationFailureCodes.ArtifactNotEligible,
                "Only completed Intake artifacts can be classified.");

        if (profile.OutputSchemaVersion != prompt.OutputSchemaVersion ||
            profile.OutputSchemaVersion != 1)
            throw IntakeConfigurationException.BadRequest(
                ClassificationFailureCodes.SchemaValidationFailed,
                "The classification profile references an unsupported or inconsistent output schema version.");

        var baseExecutionKey = BuildExecutionKey(artifact, profile, taxonomy, prompt, policy);
        var existing = retry
            ? (await repository.ListHistoryAsync(tenantId, artifactId, cancellationToken))
                .Where(item => item.ExecutionKey == baseExecutionKey ||
                               item.ExecutionKey.StartsWith(baseExecutionKey + ":", StringComparison.Ordinal))
                .OrderByDescending(item => item.AttemptNumber)
                .FirstOrDefault()
            : await repository.FindByExecutionKeyAsync(
                tenantId, baseExecutionKey, cancellationToken);
        if (existing is { Status: ClassificationStatuses.Processing })
            throw IntakeConfigurationException.Conflict(
                ClassificationFailureCodes.ConcurrencyConflict,
                "Another classification attempt is already processing this execution.");
        if (existing is not null && !retry)
            return Map(existing);
        if (existing is not null &&
            (!existing.IsRetryable ||
             existing.AttemptCount >= Math.Min(
                 policy.MaxAttempts,
                 resolved.EffectiveConfiguration.ClassificationMaxAttempts)))
            throw IntakeConfigurationException.BadRequest(
                ClassificationFailureCodes.RetryLimitExceeded,
                "The classification retry limit has been reached or the failure is not retryable.");
        var previousAttemptNumber = existing?.AttemptNumber ?? 0;
        var executionKey = existing is null
            ? baseExecutionKey
            : $"{baseExecutionKey}:{previousAttemptNumber + 1}";
        if (existing is not null)
        {
            var concurrentRetry = await repository.FindByExecutionKeyAsync(
                tenantId, executionKey, cancellationToken);
            if (concurrentRetry is { Status: ClassificationStatuses.Processing })
                throw IntakeConfigurationException.Conflict(
                    ClassificationFailureCodes.ConcurrencyConflict,
                    "Another classification retry is already processing this execution.");
            if (concurrentRetry is not null)
                return Map(concurrentRetry);
            existing = null;
        }

        var provider = providerRegistry.GetRequired(policy.ProviderCode);
        if (!provider.IsConfigured)
            throw IntakeConfigurationException.BadRequest(
                ClassificationFailureCodes.ProviderUnavailable,
                $"AI provider '{policy.ProviderCode}' is not configured.");

        var content = await contentReader.ReadAsync(
            tenantId,
            artifact,
            resolved.EffectiveConfiguration.MaxClassificationInputCharacters,
            cancellationToken);
        if (content.Success &&
            content.ObservedSha256 is not null &&
            !string.Equals(
                content.ObservedSha256,
                artifact.Sha256,
                StringComparison.OrdinalIgnoreCase))
        {
            content = content with
            {
                Success = false,
                FailureCode = ClassificationFailureCodes.ArtifactHashChanged,
                FailureMessage = "The retrieved artifact content does not match its persisted SHA-256 binding.",
                IsRetryable = true,
            };
        }
        var now = DateTimeOffset.UtcNow;
        var classification = existing ?? new ArtifactClassification
        {
            Id = Guid.CreateVersion7(),
            TenantId = tenantId,
            IntakeArtifactId = artifact.Id,
            ArtifactSha256 = artifact.Sha256,
            ClassificationProfileCode = profile.Code,
            ClassificationProfileVersion = profile.Version,
            TaxonomyCode = taxonomy.Code,
            TaxonomyVersion = taxonomy.Version,
            PromptCode = prompt.Code,
            PromptVersion = prompt.Version,
            OutputSchemaVersion = profile.OutputSchemaVersion,
            ProviderCode = policy.ProviderCode,
            ModelCode = policy.ModelCode,
            ExecutionKey = executionKey,
            Status = ClassificationStatuses.Pending,
            AttemptCount = 0,
            AttemptNumber = previousAttemptNumber + 1,
            IsCurrent = false,
            CreatedAt = now,
            UpdatedAt = now,
            RequestedAt = now,
        };
        if (!content.Success)
        {
            SetFailure(classification, content.FailureCode ?? ClassificationFailureCodes.UnsupportedContent,
                content.FailureMessage ?? "The artifact content could not be bounded for classification.",
                content.IsRetryable);
            if (existing is null &&
                !await repository.TryAddClassificationAsync(classification, cancellationToken))
                return Map((await repository.FindByExecutionKeyAsync(
                    tenantId, executionKey, cancellationToken))!);
            if (existing is not null)
                await repository.SaveAsync(cancellationToken);
            await RecordAuditAsync(tenantId, artifactId, classification, "classification.skipped", correlationId, actorId, cancellationToken);
            return Map(classification);
        }

        classification.InputCharacters = content.CharacterCount;
        if (existing is null &&
            !await repository.TryAddClassificationAsync(classification, cancellationToken))
        {
            var concurrent = await repository.FindByExecutionKeyAsync(
                tenantId, executionKey, cancellationToken);
            if (concurrent is null || concurrent.Status == ClassificationStatuses.Processing)
                throw IntakeConfigurationException.Conflict(
                    ClassificationFailureCodes.ConcurrencyConflict,
                    "Another classification attempt is already processing this execution.");
            return Map(concurrent);
        }
        if (!await repository.TryClaimAsync(tenantId, classification.Id, retry, cancellationToken))
            throw IntakeConfigurationException.Conflict(
                ClassificationFailureCodes.ConcurrencyConflict,
                "Another classification attempt is already processing this execution.");
        classification.AttemptCount = Math.Max(1, previousAttemptNumber + 1);
        classification.AttemptNumber = classification.AttemptCount;
        classification.Status = ClassificationStatuses.Processing;
        classification.UpdatedAt = DateTimeOffset.UtcNow;
        await repository.SaveAsync(cancellationToken);

        SynqAiClassificationResult providerResult;
        var stopwatch = Stopwatch.StartNew();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(Math.Min(
            Math.Max(1, policy.TimeoutSeconds),
            Math.Max(1, resolved.EffectiveConfiguration.ClassificationTimeoutSeconds))));
        try
        {
            providerResult = await provider.ClassifyAsync(
                new SynqAiClassificationRequest(
                    tenantId,
                    policy.ModelCode,
                    prompt.InstructionText,
                    content.Text!,
                    artifact.EffectiveFileName,
                    artifact.DeclaredContentType,
                    taxonomy.ClassesJson,
                    prompt.OutputSchemaJson,
                     Math.Min(
                         Math.Max(1, policy.MaxOutputTokens),
                         resolved.EffectiveConfiguration.MaxClassificationOutputTokens),
                    profile.OutputSchemaVersion,
                    correlationId ?? string.Empty),
                policy.CredentialReference ?? "secret://platform/synq-ai",
                timeout.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            providerResult = new(
                false, null, null, null, [], null, null, null,
                ClassificationFailureCodes.ProviderTimeout,
                "The AI provider timed out.", true);
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                exception,
                "AI classification provider failed. Tenant={TenantId} Artifact={ArtifactId} Provider={ProviderCode}",
                tenantId, artifactId, policy.ProviderCode);
            providerResult = new(
                false, null, null, null, [], null, null, null,
                ClassificationFailureCodes.ProviderUnavailable,
                "The AI provider could not complete the request.", true);
        }
        stopwatch.Stop();

        classification.InputTokens = providerResult.InputTokens;
        classification.OutputTokens = providerResult.OutputTokens;
        classification.TotalTokens = providerResult.InputTokens.HasValue &&
            providerResult.OutputTokens.HasValue
            ? providerResult.InputTokens.Value + providerResult.OutputTokens.Value
            : null;
        classification.LatencyMs = stopwatch.ElapsedMilliseconds;
        classification.ProviderResponseId = SafeProviderResponseId(providerResult.ProviderResponseId);
        if (!providerResult.Success)
        {
            SetFailure(
                classification,
                providerResult.FailureCode ?? ClassificationFailureCodes.ProviderRejected,
                providerResult.FailureMessage ?? "The AI provider rejected the classification request.",
                providerResult.IsRetryable);
        }
        else
        {
            var selected = taxonomyClasses.FirstOrDefault(item =>
                string.Equals(item.Code, providerResult.ClassificationCode, StringComparison.OrdinalIgnoreCase));
            if (!providerResult.SchemaValid ||
                selected is null ||
                providerResult.Confidence is null or < 0 or > 1 ||
                string.IsNullOrWhiteSpace(providerResult.ClassificationLabel) ||
                providerResult.SafeEvidence.Count > 3 ||
                providerResult.SafeEvidence.Any(item => item.Length > 160))
            {
                SetFailure(
                    classification,
                    ClassificationFailureCodes.SchemaValidationFailed,
                    "The AI provider response did not match the versioned classification taxonomy.",
                    false);
            }
            else
            {
                classification.Status = ClassificationStatuses.Completed;
                classification.ClassificationCode = selected.Code;
                classification.ClassificationLabel = selected.Label;
                classification.Confidence = providerResult.Confidence;
                classification.DecisionStatus = providerResult.Confidence >=
                    resolved.EffectiveConfiguration.MinimumClassificationConfidence
                    ? ClassificationDecisionStatuses.Accepted
                    : ClassificationDecisionStatuses.LowConfidence;
                classification.Reason = ClassificationInputPolicy.BuildSafeReason(providerResult.Reason);
                classification.SafeEvidenceJson = JsonSerializer.Serialize(
                    ClassificationInputPolicy.BuildSafeEvidence(providerResult.SafeEvidence),
                    JsonOptions);
                classification.IsCurrent = true;
                classification.CurrentResultMarker = "CURRENT";
                classification.CompletedAt = DateTimeOffset.UtcNow;
                classification.UpdatedAt = classification.CompletedAt.Value;
            }
        }

        if (classification.IsCurrent)
            await repository.FinalizeCurrentAsync(
                tenantId,
                artifactId,
                classification,
                cancellationToken);
        else
            await repository.SaveAsync(cancellationToken);
        await RecordAuditAsync(
            tenantId,
            artifactId,
            classification,
            "classification.completed",
            correlationId,
            actorId,
            cancellationToken);
        return Map(classification);
    }

    public async Task<ArtifactClassificationResponse?> GetCurrentAsync(
        Guid tenantId,
        Guid artifactId,
        CancellationToken cancellationToken)
    {
        var result = await repository.FindCurrentAsync(tenantId, artifactId, cancellationToken);
        return result is null ? null : Map(result);
    }

    public async Task<IReadOnlyList<ArtifactClassificationResponse>> GetHistoryAsync(
        Guid tenantId,
        Guid artifactId,
        CancellationToken cancellationToken)
    {
        var results = await repository.ListHistoryAsync(tenantId, artifactId, cancellationToken);
        return results.Select(Map).ToArray();
    }

    private async Task RecordAuditAsync(
        Guid tenantId,
        Guid artifactId,
        ArtifactClassification classification,
        string action,
        string? correlationId,
        Guid? actorId,
        CancellationToken cancellationToken)
    {
        await auditSink.RecordAsync(
            new ClassificationAuditEntry(
                tenantId,
                artifactId,
                classification.Id,
                action,
                classification.Status,
                classification.FailureCode,
                correlationId,
                actorId),
            cancellationToken);
    }

    private static void SetFailure(
        ArtifactClassification classification,
        string code,
        string message,
        bool retryable)
    {
        classification.Status = ClassificationStatuses.Failed;
        classification.DecisionStatus = ClassificationDecisionStatuses.Unclassified;
        classification.IsCurrent = false;
        classification.CurrentResultMarker = null;
        classification.FailureCode = code;
        classification.FailureMessage = message.Length > 1000 ? message[..1000] : message;
        classification.IsRetryable = retryable;
        classification.UpdatedAt = DateTimeOffset.UtcNow;
    }

    private static string? SafeProviderResponseId(string? value) =>
        value is { Length: > 128 } ? value[..128] : value;

    private static string Normalize(string? value) =>
        value?.Trim().ToUpperInvariant() ?? string.Empty;

    private static string BuildExecutionKey(
        IntakeArtifact artifact,
        ClassificationProfileDefinition profile,
        ClassificationTaxonomyDefinition taxonomy,
        ClassificationPromptDefinition prompt,
        TenantAiPolicy policy)
    {
        var identity = string.Join(
            "|",
            artifact.Id,
            artifact.Sha256,
            profile.Code,
            profile.Version,
            taxonomy.Code,
            taxonomy.Version,
            prompt.Code,
            prompt.Version,
            policy.ProviderCode,
            policy.ModelCode);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identity))).ToLowerInvariant();
    }

    private static bool IsTenantCredentialReference(string value, Guid tenantId) =>
        value.StartsWith($"secret://tenant/{tenantId:D}/", StringComparison.OrdinalIgnoreCase);

    private static bool IsPlatformCredentialReference(string value) =>
        value.StartsWith("secret://platform/", StringComparison.OrdinalIgnoreCase);

    private static TenantAiPolicyResponse Map(TenantAiPolicy policy) =>
        new(
            policy.TenantId,
            policy.IsEnabled,
            policy.AccessMode,
            policy.ProviderCode,
            policy.ModelCode,
            policy.CredentialReference,
            policy.MaxOutputTokens,
            policy.TimeoutSeconds,
            policy.MaxAttempts,
            policy.PolicyVersion,
            policy.UpdatedAt);

    private static ArtifactClassificationResponse Map(ArtifactClassification classification)
    {
        IReadOnlyList<string> evidence = [];
        if (!string.IsNullOrWhiteSpace(classification.SafeEvidenceJson))
        {
            try
            {
                evidence = JsonSerializer.Deserialize<string[]>(
                    classification.SafeEvidenceJson,
                    JsonOptions) ?? [];
            }
            catch (JsonException)
            {
                evidence = [];
            }
        }

        return new(
            classification.Id,
            classification.TenantId,
            classification.IntakeArtifactId,
            classification.ArtifactSha256,
            classification.ClassificationProfileCode,
            classification.ClassificationProfileVersion,
            classification.TaxonomyCode,
            classification.TaxonomyVersion,
            classification.PromptCode,
            classification.PromptVersion,
            classification.OutputSchemaVersion,
            classification.ProviderCode,
            classification.ModelCode,
            classification.ExecutionKey,
            classification.ProviderResponseId,
            classification.Status,
            classification.DecisionStatus,
            classification.ClassificationCode,
            classification.ClassificationLabel,
            classification.Confidence,
            classification.Reason,
            evidence,
            classification.InputCharacters,
            classification.InputTokens,
            classification.OutputTokens,
            classification.TotalTokens,
            classification.LatencyMs,
            classification.FailureCode,
            classification.FailureMessage,
            classification.IsRetryable,
            classification.IsCurrent,
            classification.AttemptCount,
            classification.AttemptNumber,
            classification.RequestedAt,
            classification.CreatedAt,
            classification.UpdatedAt,
            classification.CompletedAt);
    }
}