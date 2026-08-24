using Intake.Application.Classification;
using Intake.Application.Configuration;
using Intake.Application.Extraction;
using Intake.Application.Matching;
using Intake.Application.Normalization;
using Intake.Application.Policy;
using Intake.Contracts.Configuration;
using Intake.Domain.Artifacts;
using Intake.Domain.Classification;
using Intake.Domain.Extraction;
using Intake.Domain.Matching;
using Intake.Domain.Normalization;
using Intake.Domain.Policy;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Intake.Tests;

public sealed class PolicyServiceTests
{
    [Fact]
    public async Task Cancellation_finalizes_failed_history_and_retry_can_complete()
    {
        using var cancellation = new CancellationTokenSource();
        var cancelOnce = new CancelOnceRule(cancellation);
        var policyRepository = new FakePolicyRepository();
        var classification = new ArtifactClassification
        {
            Id = Guid.NewGuid(),
            TenantId = TenantId,
            IntakeArtifactId = ArtifactId,
            Status = ClassificationStatuses.Completed,
            ClassificationCode = "LIEN_DOCUMENT",
            Confidence = 0.95,
        };
        var extraction = new ArtifactExtraction
        {
            Id = Guid.NewGuid(),
            TenantId = TenantId,
            IntakeArtifactId = ArtifactId,
            ClassificationId = classification.Id,
            ClassificationCode = "LIEN_DOCUMENT",
            Status = ExtractionStatuses.Completed,
        };
        var normalization = new ArtifactNormalization
        {
            Id = Guid.NewGuid(),
            TenantId = TenantId,
            IntakeArtifactId = ArtifactId,
            ArtifactExtractionId = extraction.Id,
            Status = NormalizationRunStatuses.Completed,
        };
        var matching = new ArtifactMatchRun
        {
            Id = Guid.NewGuid(),
            TenantId = TenantId,
            IntakeArtifactId = ArtifactId,
            ArtifactNormalizationId = normalization.Id,
            Status = MatchRunStatuses.Completed,
        };
        var service = new PolicyService(
            policyRepository,
            new FakeClassificationRepository(classification),
            new FakeExtractionRepository(extraction),
            new FakeNormalizationRepository(normalization),
            new FakeMatchingRepository(matching),
            new FakeConfigurationService(),
            new PolicyRuleRegistry([cancelOnce]),
            new RecordingAuditSink(),
            NullLogger<PolicyService>.Instance);

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            service.EvaluateAsync(
                TenantId,
                ArtifactId,
                null,
                null,
                "cancel-test",
                false,
                cancellation.Token));

        var cancelled = Assert.Single(policyRepository.History);
        Assert.Equal(PolicyEvaluationStatuses.Failed, cancelled.Status);
        Assert.Equal(
            PolicyFailureCodes.ExecutionCancelled,
            cancelled.FailureCode);

        cancelOnce.ShouldCancel = false;
        var retry = await service.EvaluateAsync(
            TenantId,
            ArtifactId,
            null,
            null,
            "retry-test",
            true,
            CancellationToken.None);

        Assert.Equal(PolicyEvaluationStatuses.Completed, retry.Status);
        Assert.True(retry.IsCurrent);
        Assert.Equal(2, policyRepository.History.Count);
        Assert.False(policyRepository.History.Single(item => item.Id == cancelled.Id).IsCurrent);
    }

    private sealed class CancelOnceRule(CancellationTokenSource cancellation)
        : IPolicyRule
    {
        public string Code => "TEST_CANCEL";
        public int Order => 1;
        public bool ShouldCancel { get; set; } = true;

        public void Evaluate(
            PolicyRuleContext context,
            PolicyEvaluationState state)
        {
            if (!ShouldCancel)
                return;
            ShouldCancel = false;
            cancellation.Cancel();
            throw new OperationCanceledException(cancellation.Token);
        }
    }

    private sealed class FakePolicyRepository : IArtifactPolicyRepository
    {
        public List<ArtifactPolicyEvaluation> History { get; } = [];

        public Task<PolicyProfileDefinition?> FindProfileAsync(
            string code,
            int? version,
            CancellationToken cancellationToken) =>
            Task.FromResult<PolicyProfileDefinition?>(
                new PolicyProfileDefinition
                {
                    Id = PolicyDefinitionIds.LienIntakePolicyV1,
                    Code = PolicyProfileDefaults.Code,
                    DisplayName = "Lien Intake Policy V1",
                    Version = PolicyProfileDefaults.Version,
                    IsActive = true,
                    DefinitionJson = PolicyProfileDefaults.DefinitionJson,
                });

        public Task<IReadOnlyList<PolicyProfileDefinition>> ListProfilesAsync(
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<PolicyProfileDefinition>>([]);

        public Task<ArtifactPolicyEvaluation?> FindCurrentAsync(
            Guid tenantId,
            Guid artifactId,
            CancellationToken cancellationToken) =>
            Task.FromResult(
                History.SingleOrDefault(item =>
                    item.TenantId == tenantId &&
                    item.ArtifactId == artifactId &&
                    item.IsCurrent));

        public Task<ArtifactPolicyEvaluation?> FindByExecutionKeyAsync(
            Guid tenantId,
            string executionKey,
            CancellationToken cancellationToken) =>
            Task.FromResult(
                History.SingleOrDefault(item =>
                    item.TenantId == tenantId &&
                    item.ExecutionKey == executionKey));

        public Task<IReadOnlyList<ArtifactPolicyEvaluation>> ListHistoryAsync(
            Guid tenantId,
            Guid artifactId,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ArtifactPolicyEvaluation>>(
                History.Where(item =>
                    item.TenantId == tenantId &&
                    item.ArtifactId == artifactId)
                .OrderByDescending(item => item.CreatedAt)
                .ToArray());

        public Task<bool> TryAddEvaluationAsync(
            ArtifactPolicyEvaluation evaluation,
            CancellationToken cancellationToken)
        {
            History.Add(evaluation);
            return Task.FromResult(true);
        }

        public Task FinalizeCurrentAsync(
            Guid tenantId,
            Guid artifactId,
            ArtifactPolicyEvaluation evaluation,
            IReadOnlyList<ArtifactPolicyFinding> findings,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            foreach (var current in History.Where(item =>
                         item.TenantId == tenantId &&
                         item.ArtifactId == artifactId))
                current.IsCurrent = false;
            evaluation.IsCurrent = true;
            evaluation.CurrentResultMarker = "CURRENT";
            evaluation.Findings = findings.ToList();
            return Task.CompletedTask;
        }

        public Task SaveAsync(CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }

    private sealed class FakeConfigurationService : IIntakeConfigurationService
    {
        public Task<ResolvedProcessingConfiguration> ResolveAsync(
            Guid tenantId,
            string? profileCode,
            CancellationToken cancellationToken) =>
            Task.FromResult(new ResolvedProcessingConfiguration(
                tenantId,
                ProcessingProfileCodes.LienIntakeV1,
                1,
                1,
                1,
                new LienIntakeV1Configuration
                {
                    EnablePatientMatching = false,
                    EnableFacilityMatching = false,
                    RequirePatientMatch = false,
                    RequireProviderOrFacilityMatch = false,
                    EnableAutoAcceptableDisposition = false,
                    AllowAutoApproval = false,
                },
                DateTimeOffset.UtcNow));

        public Task<TenantIntakeConfigurationResponse?> GetConfigurationAsync(
            Guid tenantId,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();
        public Task<TenantIntakeConfigurationResponse> UpsertConfigurationAsync(
            Guid tenantId,
            UpsertTenantIntakeConfigurationRequest request,
            Guid? actorId,
            string? correlationId,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();
        public Task<IReadOnlyList<ProcessingProfileDefinitionResponse>> ListAvailableProfilesAsync(
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();
        public Task<IReadOnlyList<TenantProcessingProfileResponse>> ListTenantProfilesAsync(
            Guid tenantId,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();
        public Task<TenantProcessingProfileResponse> AssignProfileAsync(
            Guid tenantId,
            AssignTenantProcessingProfileRequest request,
            Guid? actorId,
            string? correlationId,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();
        public Task<TenantProcessingProfileResponse?> GetTenantProfileAsync(
            Guid tenantId,
            string profileCode,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();
        public Task<TenantProcessingProfileResponse> UpdateTenantProfileAsync(
            Guid tenantId,
            string profileCode,
            UpdateTenantProcessingProfileRequest request,
            Guid? actorId,
            string? correlationId,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();
        public Task<TenantProcessingProfileResponse> UpdateTenantProfileStatusAsync(
            Guid tenantId,
            string profileCode,
            UpdateTenantProcessingProfileStatusRequest request,
            Guid? actorId,
            string? correlationId,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class FakeClassificationRepository(
        ArtifactClassification current) : IClassificationRepository
    {
        public Task<IntakeArtifact?> FindArtifactAsync(
            Guid tenantId,
            Guid artifactId,
            CancellationToken cancellationToken) =>
            Task.FromResult<IntakeArtifact?>(
                new IntakeArtifact
                {
                    Id = artifactId,
                    TenantId = tenantId,
                });
        public Task<ArtifactClassification?> FindCurrentAsync(
            Guid tenantId,
            Guid artifactId,
            CancellationToken cancellationToken) =>
            Task.FromResult<ArtifactClassification?>(current);
        public Task<TenantAiPolicy?> FindPolicyAsync(
            Guid tenantId,
            CancellationToken cancellationToken) =>
            Task.FromResult<TenantAiPolicy?>(null);
        public Task SavePolicyAsync(
            TenantAiPolicy policy,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();
        public Task<ClassificationProfileDefinition?> FindProfileAsync(
            string code,
            int? version,
            CancellationToken cancellationToken) =>
            Task.FromResult<ClassificationProfileDefinition?>(null);
        public Task<ClassificationTaxonomyDefinition?> FindTaxonomyAsync(
            string code,
            int version,
            CancellationToken cancellationToken) =>
            Task.FromResult<ClassificationTaxonomyDefinition?>(null);
        public Task<ClassificationPromptDefinition?> FindPromptAsync(
            string code,
            int version,
            CancellationToken cancellationToken) =>
            Task.FromResult<ClassificationPromptDefinition?>(null);
        public Task<IReadOnlyList<ClassificationProfileDefinition>> ListProfilesAsync(
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ClassificationProfileDefinition>>([]);
        public Task<ArtifactClassification?> FindByExecutionKeyAsync(
            Guid tenantId,
            string executionKey,
            CancellationToken cancellationToken) =>
            Task.FromResult<ArtifactClassification?>(null);
        public Task<IReadOnlyList<ArtifactClassification>> ListHistoryAsync(
            Guid tenantId,
            Guid artifactId,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ArtifactClassification>>([]);
        public Task<bool> TryClaimAsync(
            Guid tenantId,
            Guid classificationId,
            bool retryFailed,
            CancellationToken cancellationToken) =>
            Task.FromResult(false);
        public Task ClearCurrentAsync(
            Guid tenantId,
            Guid artifactId,
            Guid replacementClassificationId,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();
        public Task FinalizeCurrentAsync(
            Guid tenantId,
            Guid artifactId,
            ArtifactClassification classification,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();
        public Task<bool> TryAddClassificationAsync(
            ArtifactClassification classification,
            CancellationToken cancellationToken) =>
            Task.FromResult(false);
        public Task SaveAsync(CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }

    private sealed class FakeExtractionRepository(
        ArtifactExtraction current) : IArtifactExtractionRepository
    {
        public Task<ArtifactExtraction?> FindCurrentAsync(
            Guid tenantId,
            Guid artifactId,
            Guid classificationId,
            CancellationToken cancellationToken) =>
            Task.FromResult<ArtifactExtraction?>(current);
        public Task<ExtractionProfileDefinition?> FindProfileAsync(
            string code,
            int? version,
            CancellationToken cancellationToken) =>
            Task.FromResult<ExtractionProfileDefinition?>(null);
        public Task<ExtractionSchemaDefinition?> FindSchemaAsync(
            string code,
            int version,
            string classificationCode,
            CancellationToken cancellationToken) =>
            Task.FromResult<ExtractionSchemaDefinition?>(null);
        public Task<ExtractionPromptDefinition?> FindPromptAsync(
            string code,
            int version,
            string classificationCode,
            CancellationToken cancellationToken) =>
            Task.FromResult<ExtractionPromptDefinition?>(null);
        public Task<IReadOnlyList<ExtractionProfileDefinition>> ListProfilesAsync(
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ExtractionProfileDefinition>>([]);
        public Task<ArtifactExtraction?> FindByExecutionKeyAsync(
            Guid tenantId,
            string executionKey,
            CancellationToken cancellationToken) =>
            Task.FromResult<ArtifactExtraction?>(null);
        public Task<IReadOnlyList<ArtifactExtraction>> ListHistoryAsync(
            Guid tenantId,
            Guid artifactId,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ArtifactExtraction>>([]);
        public Task<bool> TryClaimAsync(
            Guid tenantId,
            Guid extractionId,
            bool retryFailed,
            CancellationToken cancellationToken) =>
            Task.FromResult(false);
        public Task FinalizeCurrentAsync(
            Guid tenantId,
            Guid artifactId,
            ArtifactExtraction extraction,
            IReadOnlyList<ArtifactExtractedFact> facts,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();
        public Task<bool> TryAddExtractionAsync(
            ArtifactExtraction extraction,
            CancellationToken cancellationToken) =>
            Task.FromResult(false);
        public Task SaveAsync(CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }

    private sealed class FakeNormalizationRepository(
        ArtifactNormalization current) : IArtifactNormalizationRepository
    {
        public Task<ArtifactNormalization?> FindCurrentAsync(
            Guid tenantId,
            Guid artifactId,
            Guid artifactExtractionId,
            CancellationToken cancellationToken) =>
            Task.FromResult<ArtifactNormalization?>(current);
        public Task<NormalizationProfileDefinition?> FindProfileAsync(
            string code,
            int? version,
            CancellationToken cancellationToken) =>
            Task.FromResult<NormalizationProfileDefinition?>(null);
        public Task<IReadOnlyList<NormalizationProfileDefinition>> ListProfilesAsync(
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<NormalizationProfileDefinition>>([]);
        public Task<ArtifactNormalization?> FindByExecutionKeyAsync(
            Guid tenantId,
            string executionKey,
            CancellationToken cancellationToken) =>
            Task.FromResult<ArtifactNormalization?>(null);
        public Task<IReadOnlyList<ArtifactNormalization>> ListHistoryAsync(
            Guid tenantId,
            Guid artifactId,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ArtifactNormalization>>([]);
        public Task<bool> TryAddNormalizationAsync(
            ArtifactNormalization normalization,
            CancellationToken cancellationToken) =>
            Task.FromResult(false);
        public Task FinalizeCurrentAsync(
            Guid tenantId,
            Guid artifactId,
            ArtifactNormalization normalization,
            IReadOnlyList<ArtifactNormalizedFact> facts,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();
        public Task SaveAsync(CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }

    private sealed class FakeMatchingRepository(
        ArtifactMatchRun current) : IArtifactMatchingRepository
    {
        public Task<ArtifactMatchRun?> FindCurrentAsync(
            Guid tenantId,
            Guid artifactId,
            Guid normalizationId,
            CancellationToken cancellationToken) =>
            Task.FromResult<ArtifactMatchRun?>(current);
        public Task<MatchingProfileDefinition?> FindProfileAsync(
            string code,
            int? version,
            CancellationToken cancellationToken) =>
            Task.FromResult<MatchingProfileDefinition?>(null);
        public Task<IReadOnlyList<MatchingProfileDefinition>> ListProfilesAsync(
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<MatchingProfileDefinition>>([]);
        public Task<ArtifactMatchRun?> FindByExecutionKeyAsync(
            Guid tenantId,
            string executionKey,
            CancellationToken cancellationToken) =>
            Task.FromResult<ArtifactMatchRun?>(null);
        public Task<IReadOnlyList<ArtifactMatchRun>> ListHistoryAsync(
            Guid tenantId,
            Guid artifactId,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ArtifactMatchRun>>([]);
        public Task<ArtifactMatchRun?> FindBusinessDuplicateRunAsync(
            Guid tenantId,
            string businessKeyFingerprint,
            Guid excludedArtifactId,
            CancellationToken cancellationToken) =>
            Task.FromResult<ArtifactMatchRun?>(null);
        public Task<bool> TryAddMatchRunAsync(
            ArtifactMatchRun run,
            CancellationToken cancellationToken) =>
            Task.FromResult(false);
        public Task FinalizeCurrentAsync(
            Guid tenantId,
            Guid artifactId,
            ArtifactMatchRun run,
            IReadOnlyList<ArtifactEntityMatch> entityMatches,
            IReadOnlyList<ArtifactMatchField> fields,
            IReadOnlyList<ArtifactDuplicateSignal> duplicateSignals,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();
        public Task SaveAsync(CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }

    private sealed class RecordingAuditSink : IPolicyAuditSink
    {
        public Task RecordAsync(
            PolicyAuditEntry entry,
            CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }

    private static readonly Guid TenantId =
        new("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa");
    private static readonly Guid ArtifactId =
        new("bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb");
}