using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Intake.Application.Artifacts;
using Intake.Application.Classification;
using Intake.Application.Configuration;
using Intake.Application.Extraction;
using Intake.Application.Matching;
using Intake.Application.Normalization;
using Intake.Contracts.Configuration;
using Intake.Contracts.Policy;
using Intake.Domain.Classification;
using Intake.Domain.Extraction;
using Intake.Domain.Matching;
using Intake.Domain.Normalization;
using Intake.Domain.Policy;
using Microsoft.Extensions.Logging;

namespace Intake.Application.Policy;

public sealed class PolicyService(
    IArtifactPolicyRepository repository,
    IClassificationRepository classificationRepository,
    IArtifactExtractionRepository extractionRepository,
    IArtifactNormalizationRepository normalizationRepository,
    IArtifactMatchingRepository matchingRepository,
    IIntakeConfigurationService configurationService,
    IPolicyRuleRegistry ruleRegistry,
    IPolicyAuditSink auditSink,
    ILogger<PolicyService> logger) : IArtifactPolicyService
{
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    public async Task<IReadOnlyList<PolicyProfileResponse>> ListProfilesAsync(
        CancellationToken cancellationToken) =>
        (await repository.ListProfilesAsync(cancellationToken))
        .Select(profile => new PolicyProfileResponse(
            profile.Code,
            profile.DisplayName,
            profile.Description,
            profile.Version,
            profile.IsActive,
            profile.IsSystemDefined))
        .ToArray();

    public async Task<ArtifactPolicyResponse?> GetCurrentAsync(
        Guid tenantId,
        Guid artifactId,
        CancellationToken cancellationToken) =>
        Map(await repository.FindCurrentAsync(tenantId, artifactId, cancellationToken));

    public async Task<IReadOnlyList<ArtifactPolicyResponse>> GetHistoryAsync(
        Guid tenantId,
        Guid artifactId,
        CancellationToken cancellationToken) =>
        (await repository.ListHistoryAsync(tenantId, artifactId, cancellationToken))
        .Select(item => Map(item)!)
        .ToArray();

    public async Task<ArtifactPolicyResponse> EvaluateAsync(
        Guid tenantId,
        Guid artifactId,
        string? processingProfileCode,
        Guid? actorId,
        string? correlationId,
        bool retry,
        CancellationToken cancellationToken)
    {
        if (tenantId == Guid.Empty || artifactId == Guid.Empty)
            throw IntakeConfigurationException.Forbidden(
                PolicyFailureCodes.TenantContextInvalid,
                "A valid tenant and artifact context are required.");

        var resolved = await configurationService.ResolveAsync(
            tenantId,
            processingProfileCode,
            cancellationToken);
        var configuration = resolved.EffectiveConfiguration;
        if (!configuration.EnablePolicyEvaluation)
            throw IntakeConfigurationException.BadRequest(
                PolicyFailureCodes.ConfigurationInvalid,
                "Policy evaluation is disabled by the tenant processing profile.");

        var profileCode = configuration.PolicyProfileCode.Trim().ToUpperInvariant();
        var profileEntity = await repository.FindProfileAsync(
            profileCode,
            null,
            cancellationToken);
        if (profileEntity is null)
            throw IntakeConfigurationException.BadRequest(
                PolicyFailureCodes.ProfileUnavailable,
                $"Policy profile '{profileCode}' is not available.");

        PolicyProfileDocument profile;
        try
        {
            profile = PolicyProfileDefaults.Parse(profileEntity.DefinitionJson);
            PolicyProfileDefaults.Validate(profile);
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException)
        {
            throw IntakeConfigurationException.BadRequest(
                PolicyFailureCodes.ProfileUnavailable,
                $"Policy profile '{profileCode}' is invalid: {exception.Message}");
        }

        var inputs = await LoadInputsAsync(
            tenantId,
            artifactId,
            cancellationToken);
        var baseExecutionKey = BuildExecutionKey(
            tenantId,
            artifactId,
            profileEntity,
            inputs);
        var existing = retry
            ? (await repository.ListHistoryAsync(tenantId, artifactId, cancellationToken))
                .Where(item => item.ExecutionKey == baseExecutionKey ||
                               item.ExecutionKey.StartsWith(
                                   baseExecutionKey + ":",
                                   StringComparison.Ordinal))
                .OrderByDescending(item => item.CreatedAt)
                .ThenByDescending(item => item.Id)
                .FirstOrDefault()
            : await repository.FindByExecutionKeyAsync(
                tenantId,
                baseExecutionKey,
                cancellationToken);
        if (existing is { Status: PolicyEvaluationStatuses.Processing })
            throw IntakeConfigurationException.Conflict(
                PolicyFailureCodes.EvaluationFailed,
                "Another policy evaluation is already processing this execution.");
        if (existing is not null && !retry)
            return Map(existing)!;

        var executionKey = existing is null
            ? baseExecutionKey
            : $"{baseExecutionKey}:{existing.CreatedAt.UtcTicks}";
        var now = DateTimeOffset.UtcNow;
        var evaluation = new ArtifactPolicyEvaluation
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ArtifactId = artifactId,
            ClassificationId = inputs.Classification?.Id,
            ArtifactExtractionId = inputs.Extraction?.Id,
            ArtifactNormalizationId = inputs.Normalization?.Id,
            ArtifactMatchRunId = inputs.MatchRun?.Id,
            PolicyProfileCode = profileEntity.Code,
            PolicyProfileVersion = profileEntity.Version,
            Status = PolicyEvaluationStatuses.Processing,
            ExecutionKey = executionKey,
            RequestedAt = now,
            CreatedAt = now,
            UpdatedAt = now,
        };
        if (!await repository.TryAddEvaluationAsync(evaluation, cancellationToken))
        {
            var concurrent = await repository.FindByExecutionKeyAsync(
                tenantId,
                executionKey,
                cancellationToken);
            if (concurrent is not null)
                return Map(concurrent)!;
            throw IntakeConfigurationException.Conflict(
                PolicyFailureCodes.EvaluationFailed,
                "The policy evaluation could not be claimed.");
        }

        try
        {
            var state = new PolicyEvaluationState();
            AddLineageFindings(inputs, state);
            var ruleContext = new PolicyRuleContext(
                inputs,
                configuration,
                profile);
            foreach (var rule in ruleRegistry.Rules)
                rule.Evaluate(ruleContext, state);

            var confidence = PolicyConfidenceCalculator.Calculate(state, profile);
            if (confidence < (decimal)configuration.ReviewThreshold)
                state.AddFinding(new PolicyFindingDraft(
                    PolicyRuleCodes.OverallConfidence,
                    PolicyFindingCategories.Confidence,
                    PolicyFindingSeverities.Review,
                    PolicyFindingOutcomes.Triggered,
                    PolicyReasonCodes.OverallConfidenceBelowThreshold,
                    Score: confidence,
                    Threshold: (decimal)configuration.ReviewThreshold));

            var disposition = PolicyDispositionResolver.Resolve(
                state,
                confidence,
                configuration,
                profile);
            if (disposition == PolicyDispositionCodes.ReviewRequired &&
                !configuration.EnableAutoAcceptableDisposition)
                state.AddFinding(new PolicyFindingDraft(
                    PolicyRuleCodes.AutoAcceptable,
                    PolicyFindingCategories.Confidence,
                    PolicyFindingSeverities.Info,
                    PolicyFindingOutcomes.Triggered,
                    PolicyReasonCodes.AutoAcceptableDisabled));

            evaluation.Status = PolicyEvaluationStatuses.Completed;
            evaluation.Disposition = disposition;
            evaluation.OverallConfidence = confidence;
            evaluation.ReviewPriority = PolicyReviewPriorityResolver.Resolve(
                state,
                disposition);
            evaluation.CompletedAt = DateTimeOffset.UtcNow;
            evaluation.UpdatedAt = evaluation.CompletedAt.Value;
            var findings = state.Findings
                .Select(draft => ToFinding(evaluation, draft))
                .ToArray();
            await repository.FinalizeCurrentAsync(
                tenantId,
                artifactId,
                evaluation,
                findings,
                cancellationToken);
            await auditSink.RecordAsync(
                new PolicyAuditEntry(
                    "POLICY_EVALUATED",
                    tenantId,
                    artifactId,
                    evaluation.Id,
                    evaluation.PolicyProfileCode,
                    evaluation.Status,
                    evaluation.Disposition,
                    evaluation.ReviewPriority,
                    findings.Length,
                    null,
                    correlationId,
                    actorId),
                cancellationToken);
            return Map(evaluation)!;
        }
        catch (OperationCanceledException exception)
        {
            evaluation.Status = PolicyEvaluationStatuses.Failed;
            evaluation.FailureCode = PolicyFailureCodes.ExecutionCancelled;
            evaluation.FailureMessage = exception.Message is { Length: > 0 } message
                ? message[..Math.Min(message.Length, 1_000)]
                : "The policy evaluation was cancelled.";
            evaluation.UpdatedAt = DateTimeOffset.UtcNow;
            try
            {
                await repository.SaveAsync(CancellationToken.None);
                await auditSink.RecordAsync(
                    new PolicyAuditEntry(
                        "POLICY_EVALUATION_CANCELLED",
                        tenantId,
                        artifactId,
                        evaluation.Id,
                        evaluation.PolicyProfileCode,
                        evaluation.Status,
                        evaluation.Disposition,
                        evaluation.ReviewPriority,
                        0,
                        evaluation.FailureCode,
                        correlationId,
                        actorId),
                    CancellationToken.None);
            }
            catch (Exception cleanupException)
            {
                logger.LogError(
                    cleanupException,
                    "Policy cancellation cleanup failed for artifact {ArtifactId}.",
                    artifactId);
            }
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Policy evaluation failed for artifact {ArtifactId}.",
                artifactId);
            evaluation.Status = PolicyEvaluationStatuses.Failed;
            evaluation.FailureCode = PolicyFailureCodes.EvaluationFailed;
            evaluation.FailureMessage = exception.Message[..Math.Min(exception.Message.Length, 1_000)];
            evaluation.UpdatedAt = DateTimeOffset.UtcNow;
            await repository.SaveAsync(cancellationToken);
            await auditSink.RecordAsync(
                new PolicyAuditEntry(
                    "POLICY_EVALUATION_FAILED",
                    tenantId,
                    artifactId,
                    evaluation.Id,
                    evaluation.PolicyProfileCode,
                    evaluation.Status,
                    evaluation.Disposition,
                    evaluation.ReviewPriority,
                    0,
                    evaluation.FailureCode,
                    correlationId,
                    actorId),
                cancellationToken);
            throw;
        }
    }

    private async Task<PolicyEvaluationContext> LoadInputsAsync(
        Guid tenantId,
        Guid artifactId,
        CancellationToken cancellationToken)
    {
        var artifact = await classificationRepository.FindArtifactAsync(
            tenantId,
            artifactId,
            cancellationToken)
            ?? throw IntakeConfigurationException.NotFound(
                "ARTIFACT_NOT_FOUND",
                "The Intake artifact was not found for this tenant.");
        var classification = await classificationRepository.FindCurrentAsync(
            tenantId,
            artifactId,
            cancellationToken);
        var extraction = classification is null
            ? null
            : await extractionRepository.FindCurrentAsync(
                tenantId,
                artifactId,
                classification.Id,
                cancellationToken);
        var normalization = extraction is null
            ? null
            : await normalizationRepository.FindCurrentAsync(
                tenantId,
                artifactId,
                extraction.Id,
                cancellationToken);
        var matchRun = normalization is null
            ? null
            : await matchingRepository.FindCurrentAsync(
                tenantId,
                artifactId,
                normalization.Id,
                cancellationToken);
        return new PolicyEvaluationContext(
            tenantId,
            artifact,
            classification,
            extraction,
            normalization,
            matchRun);
    }

    private static void AddLineageFindings(
        PolicyEvaluationContext inputs,
        PolicyEvaluationState state)
    {
        if (inputs.Classification is { } classification &&
            (classification.TenantId != inputs.TenantId ||
             classification.IntakeArtifactId != inputs.Artifact.Id))
            state.AddFinding(new PolicyFindingDraft(
                PolicyRuleCodes.StructuralValidity,
                PolicyFindingCategories.Eligibility,
                PolicyFindingSeverities.Blocking,
                PolicyFindingOutcomes.Triggered,
                PolicyReasonCodes.UpstreamMismatch));
        if (inputs.Extraction is { } extraction &&
            (extraction.TenantId != inputs.TenantId ||
             extraction.IntakeArtifactId != inputs.Artifact.Id ||
             inputs.Classification is null ||
             extraction.ClassificationId != inputs.Classification.Id))
            state.AddFinding(new PolicyFindingDraft(
                PolicyRuleCodes.StructuralValidity,
                PolicyFindingCategories.Eligibility,
                PolicyFindingSeverities.Blocking,
                PolicyFindingOutcomes.Triggered,
                PolicyReasonCodes.UpstreamMismatch));
        if (inputs.Normalization is { } normalization &&
            (normalization.TenantId != inputs.TenantId ||
             normalization.IntakeArtifactId != inputs.Artifact.Id ||
             inputs.Extraction is null ||
             normalization.ArtifactExtractionId != inputs.Extraction.Id))
            state.AddFinding(new PolicyFindingDraft(
                PolicyRuleCodes.StructuralValidity,
                PolicyFindingCategories.Eligibility,
                PolicyFindingSeverities.Blocking,
                PolicyFindingOutcomes.Triggered,
                PolicyReasonCodes.UpstreamMismatch));
        if (inputs.MatchRun is { } match &&
            (match.TenantId != inputs.TenantId ||
             match.IntakeArtifactId != inputs.Artifact.Id ||
             inputs.Normalization is null ||
             match.ArtifactNormalizationId != inputs.Normalization.Id))
            state.AddFinding(new PolicyFindingDraft(
                PolicyRuleCodes.StructuralValidity,
                PolicyFindingCategories.Eligibility,
                PolicyFindingSeverities.Blocking,
                PolicyFindingOutcomes.Triggered,
                PolicyReasonCodes.UpstreamMismatch));
    }

    private static string BuildExecutionKey(
        Guid tenantId,
        Guid artifactId,
        PolicyProfileDefinition profile,
        PolicyEvaluationContext inputs)
    {
        var source = string.Join(
            "|",
            tenantId,
            artifactId,
            profile.Code,
            profile.Version,
            inputs.Classification?.Id,
            inputs.Extraction?.Id,
            inputs.Normalization?.Id,
            inputs.MatchRun?.Id);
        return Convert.ToHexString(
                SHA256.HashData(Encoding.UTF8.GetBytes(source)))
            .ToLowerInvariant();
    }

    private static ArtifactPolicyFinding ToFinding(
        ArtifactPolicyEvaluation evaluation,
        PolicyFindingDraft draft) =>
        new()
        {
            Id = Guid.NewGuid(),
            TenantId = evaluation.TenantId,
            ArtifactPolicyEvaluationId = evaluation.Id,
            RuleCode = draft.RuleCode,
            RuleCategory = draft.RuleCategory,
            Severity = draft.Severity,
            Outcome = draft.Outcome,
            ReasonCode = draft.ReasonCode,
            EntityType = draft.EntityType,
            FactCode = draft.FactCode,
            RelatedEntityMatchId = draft.RelatedEntityMatchId,
            RelatedDuplicateSignalId = draft.RelatedDuplicateSignalId,
            RelatedNormalizedFactId = draft.RelatedNormalizedFactId,
            Score = draft.Score,
            Threshold = draft.Threshold,
            EvidenceReferenceJson = JsonSerializer.Serialize(
                draft.EvidenceReferences ?? [],
                JsonOptions),
            CreatedAt = DateTimeOffset.UtcNow,
        };

    private static ArtifactPolicyResponse? Map(
        ArtifactPolicyEvaluation? evaluation) =>
        evaluation is null
            ? null
            : new ArtifactPolicyResponse(
                evaluation.Id,
                evaluation.ArtifactId,
                evaluation.ClassificationId,
                evaluation.ArtifactExtractionId,
                evaluation.ArtifactNormalizationId,
                evaluation.ArtifactMatchRunId,
                evaluation.PolicyProfileCode,
                evaluation.PolicyProfileVersion,
                evaluation.Status,
                evaluation.Disposition,
                evaluation.OverallConfidence,
                evaluation.ReviewPriority,
                evaluation.IsCurrent,
                evaluation.FailureCode,
                evaluation.FailureMessage,
                evaluation.RequestedAt,
                evaluation.CompletedAt,
                evaluation.Findings
                    .OrderBy(finding => finding.CreatedAt)
                    .ThenBy(finding => finding.Id)
                    .Select(finding => new PolicyFindingResponse(
                        finding.Id,
                        finding.RuleCode,
                        finding.RuleCategory,
                        finding.Severity,
                        finding.Outcome,
                        finding.ReasonCode,
                        finding.EntityType,
                        finding.FactCode,
                        finding.RelatedEntityMatchId,
                        finding.RelatedDuplicateSignalId,
                        finding.RelatedNormalizedFactId,
                        finding.Score,
                        finding.Threshold,
                        ParseEvidence(finding.EvidenceReferenceJson)))
                    .ToArray());

    private static IReadOnlyList<string> ParseEvidence(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<string[]>(json, JsonOptions) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }
}