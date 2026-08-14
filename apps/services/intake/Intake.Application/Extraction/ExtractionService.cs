using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Intake.Application.Classification;
using Intake.Application.Configuration;
using Intake.Contracts.Configuration;
using Intake.Contracts.Extraction;
using Intake.Domain.Artifacts;
using Intake.Domain.Classification;
using Intake.Domain.Configuration;
using Intake.Domain.Extraction;
using Microsoft.Extensions.Logging;

namespace Intake.Application.Extraction;

public sealed class ExtractionService(
    IArtifactExtractionRepository repository,
    IClassificationRepository classificationRepository,
    IIntakeConfigurationService configurationService,
    ISynqAiProviderRegistry providerRegistry,
    IIntakeArtifactContentReader contentReader,
    IExtractionAuditSink auditSink,
    ILogger<ExtractionService> logger) : IArtifactExtractionService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<IReadOnlyList<ExtractionProfileResponse>> ListProfilesAsync(
        CancellationToken cancellationToken)
    {
        var profiles = await repository.ListProfilesAsync(cancellationToken);
        return profiles
            .OrderBy(item => item.Code)
            .ThenByDescending(item => item.Version)
            .Select(item => new ExtractionProfileResponse(
                item.Code,
                item.DisplayName,
                item.Description,
                item.Version,
                item.SchemaCode,
                item.SchemaVersion,
                item.PromptCode,
                item.PromptVersion,
                item.OutputSchemaVersion,
                item.IsActive,
                item.IsSystemDefined))
            .ToArray();
    }

    public async Task<ArtifactExtractionResponse> ExtractAsync(
        Guid tenantId,
        Guid artifactId,
        string? processingProfileCode,
        Guid? actorId,
        string? correlationId,
        bool retry,
        CancellationToken cancellationToken)
    {
        var artifact = await classificationRepository.FindArtifactAsync(
                tenantId,
                artifactId,
                cancellationToken)
            ?? throw IntakeConfigurationException.NotFound(
                "ARTIFACT_NOT_FOUND",
                "The Intake artifact was not found for the current tenant.");
        var policy = await classificationRepository.FindPolicyAsync(tenantId, cancellationToken)
            ?? throw IntakeConfigurationException.BadRequest(
                ExtractionFailureCodes.PolicyMissing,
                "No AI policy is configured for this tenant.");
        if (!policy.IsEnabled)
            throw IntakeConfigurationException.BadRequest(
                ExtractionFailureCodes.PolicyDisabled,
                "AI extraction is disabled because the tenant AI policy is disabled.");

        var resolved = await configurationService.ResolveAsync(
            tenantId,
            processingProfileCode,
            cancellationToken);
        if (!resolved.EffectiveConfiguration.EnableExtraction)
            throw IntakeConfigurationException.BadRequest(
                ExtractionFailureCodes.ExtractionDisabled,
                "Extraction is disabled by the tenant processing profile.");

        var classification = await classificationRepository.FindCurrentAsync(
            tenantId,
            artifactId,
            cancellationToken);
        if (classification is null ||
            !string.Equals(classification.Status, ClassificationStatuses.Completed, StringComparison.Ordinal) ||
            string.IsNullOrWhiteSpace(classification.ClassificationCode))
            throw IntakeConfigurationException.BadRequest(
                ExtractionFailureCodes.ClassificationRequired,
                "Extraction requires a completed current artifact classification.");
        if (string.IsNullOrWhiteSpace(artifact.Sha256))
            throw IntakeConfigurationException.BadRequest(
                ExtractionFailureCodes.ArtifactHashMissing,
                "Extraction requires a SHA-256-bound artifact.");
        if (!string.Equals(
                classification.ArtifactSha256,
                artifact.Sha256,
                StringComparison.OrdinalIgnoreCase))
            throw IntakeConfigurationException.BadRequest(
                ExtractionFailureCodes.ArtifactHashChanged,
                "The current classification is not bound to the current artifact hash.");

        var profileCode = Normalize(resolved.EffectiveConfiguration.ExtractionProfileCode);
        var profile = await repository.FindProfileAsync(profileCode, null, cancellationToken)
            ?? throw IntakeConfigurationException.NotFound(
                ExtractionFailureCodes.ProfileMissing,
                $"Extraction profile '{profileCode}' is not available.");
        var supportedCodes = ExtractionDefinitionCatalog.SupportedClassificationCodes;
        if (!supportedCodes.Contains(classification.ClassificationCode, StringComparer.Ordinal))
            throw IntakeConfigurationException.BadRequest(
                ExtractionFailureCodes.ClassificationUnsupported,
                $"Classification '{classification.ClassificationCode}' is not supported by lien-intake extraction.");

        var schema = await repository.FindSchemaAsync(
                ExtractionDefinitionCatalog.SchemaCode(profile.SchemaCode, classification.ClassificationCode),
                profile.SchemaVersion,
                classification.ClassificationCode,
                cancellationToken)
            ?? throw IntakeConfigurationException.NotFound(
                ExtractionFailureCodes.SchemaMissing,
                "The extraction schema referenced by the profile is not available.");
        var prompt = await repository.FindPromptAsync(
                ExtractionDefinitionCatalog.PromptCode(profile.PromptCode, classification.ClassificationCode),
                profile.PromptVersion,
                classification.ClassificationCode,
                cancellationToken)
            ?? throw IntakeConfigurationException.NotFound(
                ExtractionFailureCodes.PromptMissing,
                "The extraction prompt referenced by the profile is not available.");
        if (!string.Equals(schema.ClassificationCode, classification.ClassificationCode, StringComparison.Ordinal) ||
            !string.Equals(prompt.ClassificationCode, classification.ClassificationCode, StringComparison.Ordinal) ||
            profile.OutputSchemaVersion != schema.Version ||
            profile.OutputSchemaVersion != prompt.OutputSchemaVersion)
            throw IntakeConfigurationException.BadRequest(
                ExtractionFailureCodes.SchemaInvalid,
                "The extraction profile, prompt, schema, and current classification do not agree.");
        if (!string.Equals(
                artifact.ProcessingStatus,
                IntakeArtifactProcessingStatuses.Completed,
                StringComparison.Ordinal))
            throw IntakeConfigurationException.BadRequest(
                ExtractionFailureCodes.ArtifactNotEligible,
                "Only completed Intake artifacts can be extracted.");

        var baseExecutionKey = BuildExecutionKey(artifact, classification, profile, schema, prompt, policy);
        var existing = retry
            ? (await repository.ListHistoryAsync(tenantId, artifactId, cancellationToken))
                .Where(item => item.ExecutionKey == baseExecutionKey ||
                               item.ExecutionKey.StartsWith(baseExecutionKey + ":", StringComparison.Ordinal))
                .OrderByDescending(item => item.AttemptNumber)
                .FirstOrDefault()
            : await repository.FindByExecutionKeyAsync(tenantId, baseExecutionKey, cancellationToken);
        if (existing is { Status: ExtractionStatuses.Processing })
            throw IntakeConfigurationException.Conflict(
                ExtractionFailureCodes.ConcurrencyConflict,
                "Another extraction attempt is already processing this execution.");
        if (existing is not null && !retry)
            return Map(existing);
        if (existing is not null &&
            (!existing.IsRetryable ||
             existing.AttemptCount >= Math.Min(
                 policy.MaxAttempts,
                 resolved.EffectiveConfiguration.ExtractionMaxAttempts)))
            throw IntakeConfigurationException.BadRequest(
                ExtractionFailureCodes.RetryLimitExceeded,
                "The extraction retry limit has been reached or the failure is not retryable.");

        var previousAttemptNumber = existing?.AttemptNumber ?? 0;
        var executionKey = existing is null
            ? baseExecutionKey
            : $"{baseExecutionKey}:{previousAttemptNumber + 1}";
        if (existing is not null)
        {
            var concurrentRetry = await repository.FindByExecutionKeyAsync(
                tenantId,
                executionKey,
                cancellationToken);
            if (concurrentRetry is { Status: ExtractionStatuses.Processing })
                throw IntakeConfigurationException.Conflict(
                    ExtractionFailureCodes.ConcurrencyConflict,
                    "Another extraction retry is already processing this execution.");
            if (concurrentRetry is not null)
                return Map(concurrentRetry);
            existing = null;
        }

        var provider = providerRegistry.GetRequired(policy.ProviderCode);
        if (!provider.IsConfigured ||
            provider is not ISynqAiStructuredExtractionProvider extractionProvider)
            throw IntakeConfigurationException.BadRequest(
                ExtractionFailureCodes.ProviderUnavailable,
                $"AI provider '{policy.ProviderCode}' does not support structured extraction.");

        var content = await contentReader.ReadAsync(
            tenantId,
            artifact,
            resolved.EffectiveConfiguration.MaxExtractionInputCharacters,
            cancellationToken);
        if (content.Success &&
            content.ObservedSha256 is not null &&
            !string.Equals(content.ObservedSha256, artifact.Sha256, StringComparison.OrdinalIgnoreCase))
        {
            content = content with
            {
                Success = false,
                FailureCode = ExtractionFailureCodes.ArtifactHashChanged,
                FailureMessage = "The retrieved artifact content does not match its persisted SHA-256 binding.",
                IsRetryable = true,
            };
        }

        var now = DateTimeOffset.UtcNow;
        var extraction = existing ?? new ArtifactExtraction
        {
            Id = Guid.CreateVersion7(),
            TenantId = tenantId,
            IntakeArtifactId = artifact.Id,
            ClassificationId = classification.Id,
            ClassificationCode = classification.ClassificationCode!,
            ArtifactSha256 = artifact.Sha256,
            ExtractionProfileCode = profile.Code,
            ExtractionProfileVersion = profile.Version,
            SchemaCode = schema.Code,
            SchemaVersion = schema.Version,
            PromptCode = prompt.Code,
            PromptVersion = prompt.Version,
            OutputSchemaVersion = profile.OutputSchemaVersion,
            ProviderCode = policy.ProviderCode,
            ModelCode = policy.ModelCode,
            ExecutionKey = executionKey,
            Status = ExtractionStatuses.Pending,
            AttemptNumber = previousAttemptNumber + 1,
            CreatedAt = now,
            UpdatedAt = now,
            RequestedAt = now,
        };

        if (!content.Success)
        {
            SetFailure(
                extraction,
                content.FailureCode ?? ExtractionFailureCodes.UnsupportedContent,
                content.FailureMessage ?? "The artifact content could not be bounded for extraction.",
                content.IsRetryable);
            if (existing is null &&
                !await repository.TryAddExtractionAsync(extraction, cancellationToken))
                return Map((await repository.FindByExecutionKeyAsync(
                    tenantId,
                    executionKey,
                    cancellationToken))!);
            await repository.SaveAsync(cancellationToken);
            await RecordAuditAsync(
                tenantId,
                artifactId,
                extraction,
                "extraction.skipped",
                classification.ClassificationCode,
                correlationId,
                actorId,
                cancellationToken);
            return Map(extraction);
        }

        extraction.InputCharacters = content.CharacterCount;
        if (existing is null &&
            !await repository.TryAddExtractionAsync(extraction, cancellationToken))
        {
            var concurrent = await repository.FindByExecutionKeyAsync(
                tenantId,
                executionKey,
                cancellationToken);
            if (concurrent is null || concurrent.Status == ExtractionStatuses.Processing)
                throw IntakeConfigurationException.Conflict(
                    ExtractionFailureCodes.ConcurrencyConflict,
                    "Another extraction attempt is already processing this execution.");
            return Map(concurrent);
        }
        if (!await repository.TryClaimAsync(tenantId, extraction.Id, retry, cancellationToken))
            throw IntakeConfigurationException.Conflict(
                ExtractionFailureCodes.ConcurrencyConflict,
                "Another extraction attempt is already processing this execution.");
        extraction.AttemptCount = Math.Max(1, previousAttemptNumber + 1);
        extraction.AttemptNumber = extraction.AttemptCount;
        extraction.Status = ExtractionStatuses.Processing;
        extraction.UpdatedAt = DateTimeOffset.UtcNow;
        await repository.SaveAsync(cancellationToken);

        SynqAiExtractionResult providerResult;
        var stopwatch = Stopwatch.StartNew();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(Math.Min(
            Math.Max(1, policy.TimeoutSeconds),
            Math.Max(1, resolved.EffectiveConfiguration.ExtractionTimeoutSeconds))));
        try
        {
            providerResult = await extractionProvider.ExtractAsync(
                new SynqAiExtractionRequest(
                    tenantId,
                    policy.ModelCode,
                    prompt.InstructionText,
                    content.Text!,
                    artifact.EffectiveFileName,
                    artifact.DeclaredContentType,
                    classification.ClassificationCode!,
                    schema.FactCatalogJson,
                    schema.OutputSchemaJson,
                    Math.Min(
                        Math.Max(1, policy.MaxOutputTokens),
                        resolved.EffectiveConfiguration.MaxExtractionOutputTokens),
                    profile.OutputSchemaVersion,
                    correlationId ?? string.Empty),
                policy.CredentialReference ?? "secret://platform/synq-ai",
                timeout.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            providerResult = new(
                false,
                [],
                null,
                null,
                null,
                ExtractionFailureCodes.ProviderTimeout,
                "The AI provider timed out.",
                true);
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                exception,
                "AI extraction provider failed. Tenant={TenantId} Artifact={ArtifactId} Provider={ProviderCode}",
                tenantId,
                artifactId,
                policy.ProviderCode);
            providerResult = new(
                false,
                [],
                null,
                null,
                null,
                ExtractionFailureCodes.ProviderUnavailable,
                "The AI provider could not complete the request.",
                true);
        }
        stopwatch.Stop();

        extraction.InputTokens = providerResult.InputTokens;
        extraction.OutputTokens = providerResult.OutputTokens;
        extraction.TotalTokens = providerResult.InputTokens.HasValue &&
            providerResult.OutputTokens.HasValue
            ? providerResult.InputTokens.Value + providerResult.OutputTokens.Value
            : null;
        extraction.LatencyMs = stopwatch.ElapsedMilliseconds;
        extraction.ProviderResponseId = SafeProviderResponseId(providerResult.ProviderResponseId);
        IReadOnlyList<ArtifactExtractedFact> facts = [];
        if (!providerResult.Success)
        {
            SetFailure(
                extraction,
                providerResult.FailureCode ?? ExtractionFailureCodes.ProviderRejected,
                providerResult.FailureMessage ?? "The AI provider rejected the extraction request.",
                providerResult.IsRetryable);
        }
        else if (!providerResult.SchemaValid ||
                 !TryValidateFacts(
                     providerResult.Facts,
                     schema.FactCatalogJson,
                     resolved.EffectiveConfiguration,
                     out facts))
        {
            SetFailure(
                extraction,
                ExtractionFailureCodes.SchemaValidationFailed,
                "The AI provider response did not match the versioned extraction contract.",
                false);
        }
        else
        {
            extraction.Status = ExtractionStatuses.Completed;
            extraction.IsCurrent = true;
            extraction.CurrentResultMarker = "CURRENT";
            extraction.CompletedAt = DateTimeOffset.UtcNow;
            extraction.UpdatedAt = extraction.CompletedAt.Value;
        }

        if (extraction.IsCurrent)
            await repository.FinalizeCurrentAsync(
                tenantId,
                artifactId,
                extraction,
                facts,
                cancellationToken);
        else
            await repository.SaveAsync(cancellationToken);
        await RecordAuditAsync(
            tenantId,
            artifactId,
            extraction,
            "extraction.completed",
            classification.ClassificationCode,
            correlationId,
            actorId,
            cancellationToken);
        return Map(extraction);
    }

    public async Task<ArtifactExtractionResponse?> GetCurrentAsync(
        Guid tenantId,
        Guid artifactId,
        CancellationToken cancellationToken)
    {
        var classification = await classificationRepository.FindCurrentAsync(
            tenantId,
            artifactId,
            cancellationToken);
        if (classification is null)
            return null;
        var extraction = await repository.FindCurrentAsync(
            tenantId,
            artifactId,
            classification.Id,
            cancellationToken);
        return extraction is null ? null : Map(extraction);
    }

    public async Task<IReadOnlyList<ArtifactExtractionResponse>> GetHistoryAsync(
        Guid tenantId,
        Guid artifactId,
        CancellationToken cancellationToken) =>
        (await repository.ListHistoryAsync(tenantId, artifactId, cancellationToken))
        .Select(Map)
        .ToArray();

    private async Task RecordAuditAsync(
        Guid tenantId,
        Guid artifactId,
        ArtifactExtraction extraction,
        string action,
        string? classificationCode,
        string? correlationId,
        Guid? actorId,
        CancellationToken cancellationToken) =>
        await auditSink.RecordAsync(
            new ExtractionAuditEntry(
                tenantId,
                artifactId,
                extraction.Id,
                action,
                extraction.Status,
                extraction.FailureCode,
                classificationCode,
                correlationId,
                actorId),
            cancellationToken);

    private static bool TryValidateFacts(
        IReadOnlyList<SynqAiExtractedFact> source,
        string factCatalogJson,
        LienIntakeV1Configuration configuration,
        out IReadOnlyList<ArtifactExtractedFact> facts)
    {
        facts = [];
        try
        {
            using var catalogDocument = JsonDocument.Parse(factCatalogJson);
            var allowed = catalogDocument.RootElement.EnumerateArray()
                .Where(item => item.TryGetProperty("code", out _))
                .Select(item => item.GetProperty("code").GetString())
                .Where(code => !string.IsNullOrWhiteSpace(code))
                .ToHashSet(StringComparer.Ordinal);
            if (source.Count > configuration.MaxExtractedFacts ||
                source.Any(item =>
                    !allowed.Contains(item.FactCode) ||
                    !ExtractionFactCatalog.IsKnown(item.FactCode) ||
                    string.IsNullOrWhiteSpace(item.RawValue) ||
                    item.RawValue.Length > configuration.MaxFactValueCharacters ||
                    item.NormalizedCandidateValue?.Length > configuration.MaxFactValueCharacters ||
                    item.Confidence is < 0 or > 1 ||
                    item.SafeEvidence.Count > 3 ||
                    item.SafeEvidence.Any(evidence => evidence.Length > configuration.MaxFactEvidenceCharacters) ||
                    !string.Equals(
                        ExtractionFactCatalog.ByCode[item.FactCode].DataType,
                        item.DataType,
                        StringComparison.Ordinal)))
                return false;

            facts = source
                .Where(item => item.Confidence >= configuration.MinimumFactConfidence)
                .Select(item => new ArtifactExtractedFact
                {
                    Id = Guid.CreateVersion7(),
                    FactCode = item.FactCode,
                    DataType = item.DataType,
                    RawValue = ExtractionInputPolicy.BuildSafeValue(
                        item.RawValue,
                        configuration.MaxFactValueCharacters),
                    NormalizedCandidateValue = string.IsNullOrWhiteSpace(item.NormalizedCandidateValue)
                        ? null
                        : ExtractionInputPolicy.BuildSafeValue(
                            item.NormalizedCandidateValue,
                            configuration.MaxFactValueCharacters),
                    Confidence = item.Confidence,
                    EvidenceJson = JsonSerializer.Serialize(
                        ExtractionInputPolicy.BuildSafeEvidence(
                            item.SafeEvidence,
                            3,
                            configuration.MaxFactEvidenceCharacters),
                        JsonOptions),
                    FactOrdinal = item.FactOrdinal,
                    CreatedAt = DateTimeOffset.UtcNow,
                })
                .ToArray();
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static string BuildExecutionKey(
        IntakeArtifact artifact,
        ArtifactClassification classification,
        ExtractionProfileDefinition profile,
        ExtractionSchemaDefinition schema,
        ExtractionPromptDefinition prompt,
        TenantAiPolicy policy)
    {
        var input = string.Join(
            "|",
            artifact.TenantId,
            artifact.Id,
            artifact.Sha256,
            classification.Id,
            classification.ClassificationCode,
            profile.Code,
            profile.Version,
            schema.Code,
            schema.Version,
            prompt.Code,
            prompt.Version,
            policy.ProviderCode,
            policy.ModelCode);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(input))).ToLowerInvariant();
    }

    private static void SetFailure(
        ArtifactExtraction extraction,
        string code,
        string message,
        bool retryable)
    {
        extraction.Status = ExtractionStatuses.Failed;
        extraction.FailureCode = code;
        extraction.FailureMessage = message.Length <= 1000 ? message : message[..1000];
        extraction.IsRetryable = retryable;
        extraction.IsCurrent = false;
        extraction.CurrentResultMarker = null;
        extraction.UpdatedAt = DateTimeOffset.UtcNow;
    }

    private static ArtifactExtractionResponse Map(ArtifactExtraction extraction) =>
        new(
            extraction.Id,
            extraction.IntakeArtifactId,
            extraction.ClassificationId,
            extraction.ClassificationCode,
            extraction.ArtifactSha256,
            extraction.ExtractionProfileCode,
            extraction.ExtractionProfileVersion,
            extraction.SchemaCode,
            extraction.SchemaVersion,
            extraction.PromptCode,
            extraction.PromptVersion,
            extraction.ProviderCode,
            extraction.ModelCode,
            extraction.Status,
            extraction.FailureCode,
            extraction.FailureMessage,
            extraction.IsRetryable,
            extraction.IsCurrent,
            extraction.InputCharacters,
            extraction.InputTokens,
            extraction.OutputTokens,
            extraction.TotalTokens,
            extraction.LatencyMs,
            extraction.AttemptCount,
            extraction.AttemptNumber,
            extraction.CreatedAt,
            extraction.CompletedAt,
            extraction.Facts
                .OrderBy(item => item.FactOrdinal)
                .Select(item => new ExtractedFactResponse(
                    item.Id,
                    item.FactCode,
                    item.DataType,
                    item.RawValue,
                    item.NormalizedCandidateValue,
                    item.Confidence,
                    ParseEvidence(item.EvidenceJson),
                    item.FactOrdinal))
                .ToArray());

    private static IReadOnlyList<string> ParseEvidence(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return [];
        try
        {
            return JsonSerializer.Deserialize<string[]>(json, JsonOptions) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static string Normalize(string? value) => value?.Trim().ToUpperInvariant() ?? string.Empty;

    private static string? SafeProviderResponseId(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Length <= 128 ? value : value[..128];
}

public static class ExtractionDefinitionCatalog
{
    public static IReadOnlyList<string> SupportedClassificationCodes { get; } =
    [
        "MEDICAL_BILL",
        "MEDICAL_RECORD",
        "LIEN_DOCUMENT",
        "LETTER_OF_PROTECTION",
        "EXPLANATION_OF_BENEFITS",
        "SETTLEMENT_DOCUMENT",
        "ATTORNEY_DOCUMENT",
        "CORRESPONDENCE",
        "INSURANCE_DOCUMENT",
    ];

    public static string SchemaCode(string baseCode, string classificationCode) =>
        $"{baseCode}_{classificationCode}";

    public static string PromptCode(string baseCode, string classificationCode) =>
        $"{baseCode}_{classificationCode}";
}