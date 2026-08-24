using Intake.Application.Classification;
using Intake.Application.Configuration;
using Intake.Application.Extraction;
using Intake.Contracts.Configuration;
using Intake.Contracts.Extraction;
using Intake.Domain.Artifacts;
using Intake.Domain.Classification;
using Intake.Domain.Configuration;
using Intake.Domain.Extraction;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Intake.Tests;

public sealed class ExtractionTests
{
    [Fact]
    public void Extraction_profile_maps_only_supported_document_types()
    {
        Assert.Equal(9, ExtractionDefinitionCatalog.SupportedClassificationCodes.Count);
        Assert.Contains("MEDICAL_BILL", ExtractionDefinitionCatalog.SupportedClassificationCodes);
        Assert.Contains("INSURANCE_DOCUMENT", ExtractionDefinitionCatalog.SupportedClassificationCodes);
        Assert.DoesNotContain("IDENTIFICATION_DOCUMENT", ExtractionDefinitionCatalog.SupportedClassificationCodes);
        Assert.DoesNotContain("OTHER", ExtractionDefinitionCatalog.SupportedClassificationCodes);
        Assert.Equal(
            "LIEN_INTAKE_EXTRACTION_SCHEMA_MEDICAL_BILL",
            ExtractionDefinitionCatalog.SchemaCode(
                "LIEN_INTAKE_EXTRACTION_SCHEMA",
                "MEDICAL_BILL"));
    }

    [Fact]
    public void Extraction_configuration_rejects_unsafe_guardrails()
    {
        var registry = new ProcessingProfileRegistry();
        var exception = Assert.Throws<IntakeConfigurationException>(() =>
            registry.ValidateAndDeserialize(
                ProcessingProfileCodes.LienIntakeV1,
                """
                {
                  "minimumFactConfidence": 1.2
                }
                """));
        Assert.Equal("INVALID_EXTRACTION_GUARDRAILS", exception.Code);
    }

    [Fact]
    public async Task Extraction_preserves_raw_value_and_noncanonical_candidate()
    {
        var fixture = Fixture.Create(
        [
            new SynqAiExtractionResult(
                true,
                [
                    new SynqAiExtractedFact(
                        "LIEN_AMOUNT",
                        ExtractionFactDataTypes.Money,
                        "$12,345.67",
                        "12345.67",
                        0.98,
                        ["Total lien amount: $12,345.67"],
                        0),
                ],
                "response-1",
                20,
                8,
                null,
                null,
                false),
        ]);

        var result = await fixture.Service.ExtractAsync(
            fixture.TenantId,
            fixture.Artifact.Id,
            null,
            null,
            "extract-1",
            false,
            CancellationToken.None);

        var fact = Assert.Single(result.Facts);
        Assert.Equal("$12,345.67", fact.RawValue);
        Assert.Equal("12345.67", fact.NormalizedCandidateValue);
        Assert.Equal("LIEN_AMOUNT", fact.FactCode);
        Assert.Equal(fixture.Classification.Id, result.ClassificationId);
        Assert.Equal(fixture.Artifact.Sha256, result.ArtifactSha256);
        Assert.Equal(28, result.TotalTokens);
        Assert.True(result.IsCurrent);
    }

    [Fact]
    public async Task Extraction_is_idempotent_and_history_is_immutable_across_retry()
    {
        var fixture = Fixture.Create(
        [
            new SynqAiExtractionResult(
                false,
                [],
                "response-1",
                null,
                null,
                ExtractionFailureCodes.ProviderUnavailable,
                "temporary",
                true),
            new SynqAiExtractionResult(
                true,
                [
                    new SynqAiExtractedFact(
                        "PATIENT_NAME",
                        ExtractionFactDataTypes.Name,
                        "Jane Doe",
                        null,
                        0.91,
                        ["Patient: Jane Doe"],
                        0),
                ],
                "response-2",
                10,
                5,
                null,
                null,
                false),
        ]);

        var failed = await fixture.Service.ExtractAsync(
            fixture.TenantId,
            fixture.Artifact.Id,
            null,
            null,
            null,
            false,
            CancellationToken.None);
        var completed = await fixture.Service.ExtractAsync(
            fixture.TenantId,
            fixture.Artifact.Id,
            null,
            null,
            null,
            true,
            CancellationToken.None);
        var duplicate = await fixture.Service.ExtractAsync(
            fixture.TenantId,
            fixture.Artifact.Id,
            null,
            null,
            null,
            false,
            CancellationToken.None);

        Assert.Equal(ExtractionStatuses.Failed, failed.Status);
        Assert.Equal(ExtractionStatuses.Completed, completed.Status);
        Assert.Equal(failed.Id, duplicate.Id);
        Assert.Equal(2, fixture.Repository.Extractions.Count);
        Assert.Equal(new[] { 1, 2 }, fixture.Repository.Extractions
            .OrderBy(item => item.AttemptNumber)
            .Select(item => item.AttemptNumber)
            .ToArray());
        Assert.False(fixture.Repository.Extractions.Single(item => item.Id == failed.Id).IsCurrent);
    }

    [Fact]
    public async Task Extraction_requires_the_current_tenant_classification()
    {
        var fixture = Fixture.Create();
        var exception = await Assert.ThrowsAsync<IntakeConfigurationException>(() =>
            fixture.Service.ExtractAsync(
                Guid.NewGuid(),
                fixture.Artifact.Id,
                null,
                null,
                null,
                false,
                CancellationToken.None));
        Assert.Equal("ARTIFACT_NOT_FOUND", exception.Code);
    }

    [Fact]
    public async Task Extraction_rejects_a_classification_bound_to_a_different_artifact_hash()
    {
        var fixture = Fixture.Create();
        fixture.Classification.ArtifactSha256 = new string('b', 64);

        var exception = await Assert.ThrowsAsync<IntakeConfigurationException>(() =>
            fixture.Service.ExtractAsync(
                fixture.TenantId,
                fixture.Artifact.Id,
                null,
                null,
                null,
                false,
                CancellationToken.None));

        Assert.Equal(ExtractionFailureCodes.ArtifactHashChanged, exception.Code);
    }

    private sealed class Fixture
    {
        public Guid TenantId { get; } = Guid.NewGuid();
        public IntakeArtifact Artifact { get; }
        public ArtifactClassification Classification { get; }
        public FakeExtractionRepository Repository { get; }
        public ExtractionService Service { get; }

        private Fixture(IReadOnlyList<SynqAiExtractionResult> results)
        {
            Artifact = new IntakeArtifact
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                ArtifactType = IntakeArtifactTypes.Attachment,
                ArtifactRole = IntakeArtifactRoles.Attachment,
                ArtifactSourceType = "MANUAL",
                ArtifactKey = "manual/1",
                EffectiveFileName = "bill.txt",
                OriginalFileName = "bill.txt",
                DeclaredContentType = "text/plain",
                ProcessingStatus = IntakeArtifactProcessingStatuses.Completed,
                Sha256 = new string('a', 64),
                DocumentsServiceDocumentId = Guid.NewGuid(),
                SizeBytes = 100,
            };
            Classification = new ArtifactClassification
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                IntakeArtifactId = Artifact.Id,
                ArtifactSha256 = Artifact.Sha256!,
                ClassificationCode = "MEDICAL_BILL",
                Status = ClassificationStatuses.Completed,
                IsCurrent = true,
                CurrentResultMarker = "CURRENT",
            };
            var classificationRepository = new FakeClassificationRepository(
                TenantId,
                Artifact,
                Classification);
            Repository = new FakeExtractionRepository(TenantId);
            Service = new ExtractionService(
                Repository,
                classificationRepository,
                new FakeConfigurationService(),
                new FakeProviderRegistry(new FakeProvider(results)),
                new FakeContentReader(),
                new NoopAuditSink(),
                NullLogger<ExtractionService>.Instance);
        }

        public static Fixture Create(IReadOnlyList<SynqAiExtractionResult>? results = null) =>
            new(results ?? [
                new SynqAiExtractionResult(
                    true,
                    [
                        new SynqAiExtractedFact(
                            "PATIENT_NAME",
                            ExtractionFactDataTypes.Name,
                            "Jane Doe",
                            null,
                            0.95,
                            ["Patient: Jane Doe"],
                            0),
                    ],
                    "response-1",
                    10,
                    4,
                    null,
                    null,
                    false),
            ]);
    }

    private sealed class FakeProviderRegistry(FakeProvider provider) : ISynqAiProviderRegistry
    {
        public IReadOnlyList<string> AvailableProviderCodes => [SynqAiProviderCodes.OpenAi];
        public ISynqAiProvider GetRequired(string providerCode) => provider;
    }

    private sealed class FakeProvider(IReadOnlyList<SynqAiExtractionResult> results)
        : ISynqAiProvider, ISynqAiStructuredExtractionProvider
    {
        private int index;
        public string ProviderCode => SynqAiProviderCodes.OpenAi;
        public bool IsConfigured => true;

        public Task<SynqAiClassificationResult> ClassifyAsync(
            SynqAiClassificationRequest request,
            string credentialReference,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<SynqAiExtractionResult> ExtractAsync(
            SynqAiExtractionRequest request,
            string credentialReference,
            CancellationToken cancellationToken)
        {
            var result = results[Math.Min(index++, results.Count - 1)];
            return Task.FromResult(result);
        }
    }

    private sealed class FakeContentReader : IIntakeArtifactContentReader
    {
        public Task<ArtifactContentReadResult> ReadAsync(
            Guid tenantId,
            IntakeArtifact artifact,
            int maxCharacters,
            CancellationToken cancellationToken) =>
            Task.FromResult(new ArtifactContentReadResult(
                true,
                "Invoice total $12,345.67.",
                25,
                null,
                null,
                false));
    }

    private sealed class NoopAuditSink : IExtractionAuditSink
    {
        public Task RecordAsync(ExtractionAuditEntry entry, CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }

    private sealed class FakeClassificationRepository(
        Guid tenantId,
        IntakeArtifact artifact,
        ArtifactClassification classification) : IClassificationRepository
    {
        public Task<TenantAiPolicy?> FindPolicyAsync(Guid id, CancellationToken ct) =>
            Task.FromResult<TenantAiPolicy?>(id == tenantId
                ? new TenantAiPolicy
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    IsEnabled = true,
                    ProviderCode = SynqAiProviderCodes.OpenAi,
                    ModelCode = "gpt-test",
                    PolicyVersion = 1,
                    MaxOutputTokens = 1200,
                }
                : null);

        public Task SavePolicyAsync(TenantAiPolicy policy, CancellationToken ct) =>
            Task.CompletedTask;

        public Task<ClassificationProfileDefinition?> FindProfileAsync(string code, int? version, CancellationToken ct) =>
            Task.FromResult<ClassificationProfileDefinition?>(null);

        public Task<ClassificationTaxonomyDefinition?> FindTaxonomyAsync(string code, int version, CancellationToken ct) =>
            Task.FromResult<ClassificationTaxonomyDefinition?>(null);

        public Task<ClassificationPromptDefinition?> FindPromptAsync(string code, int version, CancellationToken ct) =>
            Task.FromResult<ClassificationPromptDefinition?>(null);

        public Task<IReadOnlyList<ClassificationProfileDefinition>> ListProfilesAsync(CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<ClassificationProfileDefinition>>([]);

        public Task<IntakeArtifact?> FindArtifactAsync(Guid id, Guid artifactId, CancellationToken ct) =>
            Task.FromResult<IntakeArtifact?>(id == tenantId && artifactId == artifact.Id ? artifact : null);

        public Task<ArtifactClassification?> FindCurrentAsync(Guid id, Guid artifactId, CancellationToken ct) =>
            Task.FromResult<ArtifactClassification?>(
                id == tenantId && artifactId == artifact.Id ? classification : null);

        public Task<ArtifactClassification?> FindByExecutionKeyAsync(Guid id, string key, CancellationToken ct) =>
            Task.FromResult<ArtifactClassification?>(null);

        public Task<IReadOnlyList<ArtifactClassification>> ListHistoryAsync(Guid id, Guid artifactId, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<ArtifactClassification>>(
                id == tenantId && artifactId == artifact.Id ? [classification] : []);

        public Task<bool> TryClaimAsync(Guid id, Guid classificationId, bool retryFailed, CancellationToken ct) =>
            Task.FromResult(false);

        public Task ClearCurrentAsync(Guid id, Guid artifactId, Guid replacementId, CancellationToken ct) =>
            Task.CompletedTask;

        public Task FinalizeCurrentAsync(Guid id, Guid artifactId, ArtifactClassification value, CancellationToken ct) =>
            Task.CompletedTask;

        public Task<bool> TryAddClassificationAsync(ArtifactClassification value, CancellationToken ct) =>
            Task.FromResult(false);

        public Task SaveAsync(CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class FakeExtractionRepository(Guid tenantId)
        : IArtifactExtractionRepository
    {
        public List<ArtifactExtraction> Extractions { get; } = [];

        public Task<ExtractionProfileDefinition?> FindProfileAsync(
            string code,
            int? version,
            CancellationToken cancellationToken) =>
            Task.FromResult<ExtractionProfileDefinition?>(
                code == "LIEN_INTAKE_EXTRACTION_V1"
                    ? new ExtractionProfileDefinition
                    {
                        Code = code,
                        Version = 1,
                        SchemaCode = "LIEN_INTAKE_EXTRACTION_SCHEMA",
                        SchemaVersion = 1,
                        PromptCode = "LIEN_INTAKE_EXTRACTION_PROMPT",
                        PromptVersion = 1,
                        OutputSchemaVersion = 1,
                        DisplayName = "Lien Intake Extraction",
                        IsActive = true,
                    }
                    : null);

        public Task<ExtractionSchemaDefinition?> FindSchemaAsync(
            string code,
            int version,
            string classificationCode,
            CancellationToken cancellationToken) =>
            Task.FromResult<ExtractionSchemaDefinition?>(new ExtractionSchemaDefinition
            {
                Code = code,
                Version = version,
                ClassificationCode = classificationCode,
                FactCatalogJson = """[{"code":"LIEN_AMOUNT","dataType":"MONEY"} ,{"code":"PATIENT_NAME","dataType":"NAME"}]""",
                OutputSchemaJson = """{"type":"object"}""",
            });

        public Task<ExtractionPromptDefinition?> FindPromptAsync(
            string code,
            int version,
            string classificationCode,
            CancellationToken cancellationToken) =>
            Task.FromResult<ExtractionPromptDefinition?>(new ExtractionPromptDefinition
            {
                Code = code,
                Version = version,
                ClassificationCode = classificationCode,
                InstructionText = "Extract source facts only.",
                OutputSchemaVersion = 1,
            });

        public Task<IReadOnlyList<ExtractionProfileDefinition>> ListProfilesAsync(CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<ExtractionProfileDefinition>>([]);

        public Task<ArtifactExtraction?> FindCurrentAsync(
            Guid tenantId,
            Guid artifactId,
            Guid classificationId,
            CancellationToken cancellationToken) =>
            Task.FromResult(
                Extractions.LastOrDefault(item =>
                    item.TenantId == tenantId &&
                    item.IntakeArtifactId == artifactId &&
                    item.ClassificationId == classificationId &&
                    item.IsCurrent));

        public Task<ArtifactExtraction?> FindByExecutionKeyAsync(
            Guid tenantId,
            string executionKey,
            CancellationToken cancellationToken) =>
            Task.FromResult(
                Extractions.LastOrDefault(item =>
                    item.TenantId == tenantId && item.ExecutionKey == executionKey));

        public Task<IReadOnlyList<ArtifactExtraction>> ListHistoryAsync(
            Guid tenantId,
            Guid artifactId,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ArtifactExtraction>>(
                Extractions.Where(item =>
                    item.TenantId == tenantId && item.IntakeArtifactId == artifactId).ToArray());

        public Task<bool> TryClaimAsync(
            Guid tenantId,
            Guid extractionId,
            bool retryFailed,
            CancellationToken cancellationToken)
        {
            var extraction = Extractions.Single(item =>
                item.TenantId == tenantId && item.Id == extractionId);
            if (extraction.Status != ExtractionStatuses.Pending &&
                !(retryFailed && extraction.Status == ExtractionStatuses.Failed && extraction.IsRetryable))
                return Task.FromResult(false);
            extraction.Status = ExtractionStatuses.Processing;
            extraction.AttemptCount++;
            return Task.FromResult(true);
        }

        public Task FinalizeCurrentAsync(
            Guid tenantId,
            Guid artifactId,
            ArtifactExtraction extraction,
            IReadOnlyList<ArtifactExtractedFact> facts,
            CancellationToken cancellationToken)
        {
            foreach (var item in Extractions.Where(item =>
                         item.TenantId == tenantId &&
                         item.IntakeArtifactId == artifactId &&
                         item.Id != extraction.Id))
            {
                item.IsCurrent = false;
                item.CurrentResultMarker = null;
            }

            extraction.Facts = facts.ToList();
            extraction.IsCurrent = true;
            extraction.CurrentResultMarker = "CURRENT";
            return Task.CompletedTask;
        }

        public Task<bool> TryAddExtractionAsync(
            ArtifactExtraction extraction,
            CancellationToken cancellationToken)
        {
            if (Extractions.Any(item => item.ExecutionKey == extraction.ExecutionKey))
                return Task.FromResult(false);
            Extractions.Add(extraction);
            return Task.FromResult(true);
        }

        public Task SaveAsync(CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }

    private sealed class FakeConfigurationService : IIntakeConfigurationService
    {
        public Task<ResolvedProcessingConfiguration> ResolveAsync(
            Guid tenantId,
            string? profileCode,
            CancellationToken ct) =>
            Task.FromResult(new ResolvedProcessingConfiguration(
                tenantId,
                "LIEN_INTAKE_V1",
                1,
                1,
                1,
                new LienIntakeV1Configuration
                {
                    EnableExtraction = true,
                    ExtractionProfileCode = "LIEN_INTAKE_EXTRACTION_V1",
                    MaxExtractionInputCharacters = 1000,
                    MaxExtractionOutputTokens = 1200,
                },
                DateTimeOffset.UtcNow));

        public Task<TenantIntakeConfigurationResponse?> GetConfigurationAsync(Guid tenantId, CancellationToken ct) => throw new NotImplementedException();
        public Task<TenantIntakeConfigurationResponse> UpsertConfigurationAsync(Guid tenantId, UpsertTenantIntakeConfigurationRequest request, Guid? actorId, string? correlationId, CancellationToken ct) => throw new NotImplementedException();
        public Task<IReadOnlyList<ProcessingProfileDefinitionResponse>> ListAvailableProfilesAsync(CancellationToken ct) => throw new NotImplementedException();
        public Task<IReadOnlyList<TenantProcessingProfileResponse>> ListTenantProfilesAsync(Guid tenantId, CancellationToken ct) => throw new NotImplementedException();
        public Task<TenantProcessingProfileResponse> AssignProfileAsync(Guid tenantId, AssignTenantProcessingProfileRequest request, Guid? actorId, string? correlationId, CancellationToken ct) => throw new NotImplementedException();
        public Task<TenantProcessingProfileResponse?> GetTenantProfileAsync(Guid tenantId, string profileCode, CancellationToken ct) => throw new NotImplementedException();
        public Task<TenantProcessingProfileResponse> UpdateTenantProfileAsync(Guid tenantId, string profileCode, UpdateTenantProcessingProfileRequest request, Guid? actorId, string? correlationId, CancellationToken ct) => throw new NotImplementedException();
        public Task<TenantProcessingProfileResponse> UpdateTenantProfileStatusAsync(Guid tenantId, string profileCode, UpdateTenantProcessingProfileStatusRequest request, Guid? actorId, string? correlationId, CancellationToken ct) => throw new NotImplementedException();
    }
}