using System.Globalization;
using System.Text.Json;
using Intake.Application.Classification;
using Intake.Application.Configuration;
using Intake.Application.Extraction;
using Intake.Application.Normalization;
using Intake.Contracts.Configuration;
using Intake.Contracts.Normalization;
using Intake.Domain.Artifacts;
using Intake.Domain.Classification;
using Intake.Domain.Configuration;
using Intake.Domain.Extraction;
using Intake.Domain.Normalization;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Intake.Tests;

public sealed class NormalizationTests
{
    private static FactNormalizationOptions Options(
        bool allowAmbiguousDates = false) =>
        new(
            "US",
            "USD",
            CultureInfo.GetCultureInfo("en-US"),
            allowAmbiguousDates,
            "NFKC",
            "1");

    private static IFactNormalizerRegistry Registry() =>
        new FactNormalizerRegistry(
        [
            new PersonNameNormalizer(),
            new OrganizationNormalizer(),
            new DateNormalizer(),
            new MoneyNormalizer(),
            new PhoneNormalizer(),
            new EmailNormalizer(),
            new AddressNormalizer(),
            new IdentifierNormalizer(),
            new TextNormalizer(),
        ]);

    [Fact]
    public void Person_names_preserve_diacritics_and_produce_comparison_keys()
    {
        var normalizer = new PersonNameNormalizer();

        var result = normalizer.Normalize(new(
            "PATIENT_NAME",
            ExtractionFactDataTypes.Name,
            "José García",
            Options()));

        Assert.Equal("José García", result.NormalizedValue);
        Assert.Equal("JOSEGARCIA", result.ComparisonKey);
        Assert.Equal(NormalizationStatuses.Normalized, result.NormalizationStatus);

        var commaName = normalizer.Normalize(new(
            "ATTORNEY_NAME",
            ExtractionFactDataTypes.Name,
            "O'Connor, Anne-Marie",
            Options()));
        using var json = JsonDocument.Parse(commaName.NormalizedJson!);
        Assert.Equal("O'Connor", json.RootElement.GetProperty("lastName").GetString());
        Assert.Equal("Anne-Marie", json.RootElement.GetProperty("firstName").GetString());
    }

    [Fact]
    public void Ambiguous_multi_token_names_are_partial_without_fabricating_truth()
    {
        var result = new PersonNameNormalizer().Normalize(new(
            "PATIENT_NAME",
            ExtractionFactDataTypes.Name,
            "Maria De La Cruz",
            Options()));

        Assert.Equal(NormalizationStatuses.Partial, result.NormalizationStatus);
        Assert.Equal(ValidationStatuses.Ambiguous, result.ValidationStatus);
        Assert.Contains(NormalizationWarningCodes.NameComponentsPartial, result.WarningCodes);
        Assert.Equal("Maria De La Cruz", result.NormalizedValue);
    }

    [Fact]
    public void Organizations_standardize_suffixes_without_expanding_abbreviations()
    {
        var normalizer = new OrganizationNormalizer();

        var suffix = normalizer.Normalize(new(
            "PROVIDER_NAME",
            ExtractionFactDataTypes.Name,
            "Acme Medical, P.C.",
            Options()));
        var abbreviation = normalizer.Normalize(new(
            "PROVIDER_NAME",
            ExtractionFactDataTypes.Name,
            "Sunrise Imaging Ctr.",
            Options()));

        Assert.Equal("Acme Medical, PC", suffix.NormalizedValue);
        Assert.Equal("Sunrise Imaging Ctr.", abbreviation.NormalizedValue);
        Assert.Equal("ACMEMEDICALPC", suffix.ComparisonKey);
    }

    [Fact]
    public void Dates_use_configured_culture_and_reject_invalid_values()
    {
        var normalizer = new DateNormalizer();

        var date = normalizer.Normalize(new(
            "DOCUMENT_DATE",
            ExtractionFactDataTypes.Date,
            "03/04/2026",
            Options()));
        var invalid = normalizer.Normalize(new(
            "DOCUMENT_DATE",
            ExtractionFactDataTypes.Date,
            "02/30/2026",
            Options()));

        Assert.Equal("2026-03-04", date.NormalizedValue);
        Assert.Contains(NormalizationWarningCodes.DateCultureApplied, date.WarningCodes);
        Assert.Equal(NormalizationStatuses.Invalid, invalid.NormalizationStatus);
        Assert.Equal(ValidationStatuses.InvalidFormat, invalid.ValidationStatus);
    }

    [Fact]
    public void Money_preserves_currency_semantics_and_warns_on_assumptions()
    {
        var normalizer = new MoneyNormalizer();

        var assumed = normalizer.Normalize(new(
            "LIEN_AMOUNT",
            ExtractionFactDataTypes.Money,
            "100",
            Options()));
        var explicitCurrency = normalizer.Normalize(new(
            "BILLED_AMOUNT",
            ExtractionFactDataTypes.Money,
            "EUR 250.00",
            Options()));

        Assert.Equal("100.00", assumed.NormalizedValue);
        Assert.Contains(NormalizationWarningCodes.CurrencyAssumed, assumed.WarningCodes);
        Assert.Contains("\"currencyCode\":\"USD\"", assumed.NormalizedJson);
        Assert.Equal("250.00", explicitCurrency.NormalizedValue);
        Assert.Contains("\"currencyCode\":\"EUR\"", explicitCurrency.NormalizedJson);
        Assert.DoesNotContain("USD", explicitCurrency.NormalizedJson);
    }

    [Fact]
    public void Phone_email_address_identifier_and_text_normalizers_are_conservative()
    {
        var registry = Registry();
        Assert.True(registry.TryResolve("PROVIDER_PHONE", "PHONE", out var phone));
        Assert.True(registry.TryResolve("ATTORNEY_EMAIL", "EMAIL", out var email));
        Assert.True(registry.TryResolve("FACILITY_ADDRESS", ExtractionFactDataTypes.Address, out var address));
        Assert.True(registry.TryResolve("CLAIM_NUMBER", ExtractionFactDataTypes.Identifier, out var identifier));
        Assert.True(registry.TryResolve("DOCUMENT_TITLE", ExtractionFactDataTypes.Text, out var text));

        var phoneResult = phone.Normalize(new("PROVIDER_PHONE", "PHONE", "702-555-1212 ext 45", Options()));
        var emailResult = email.Normalize(new("ATTORNEY_EMAIL", "EMAIL", " Attorney@Example.COM ", Options()));
        var addressResult = address.Normalize(new(
            "FACILITY_ADDRESS",
            ExtractionFactDataTypes.Address,
            "123 Main St, Las Vegas, NV 89101",
            Options()));
        var identifierResult = identifier.Normalize(new(
            "CLAIM_NUMBER",
            ExtractionFactDataTypes.Identifier,
            " 24-CV-98531 ",
            Options()));
        var textResult = text.Normalize(new(
            "DOCUMENT_TITLE",
            ExtractionFactDataTypes.Text,
            "  A\t title\nwith  spaces\u0001 ",
            Options()));

        Assert.Equal("+17025551212", phoneResult.NormalizedValue);
        Assert.Contains(NormalizationWarningCodes.PhoneCountryAssumed, phoneResult.WarningCodes);
        Assert.Contains("\"extension\":\"45\"", phoneResult.NormalizedJson);
        Assert.Equal("Attorney@example.com", emailResult.NormalizedValue);
        Assert.Equal(NormalizationStatuses.Invalid, email.Normalize(new(
            "ATTORNEY_EMAIL", "EMAIL", "invalid-address", Options())).NormalizationStatus);
        Assert.Contains("Las Vegas", addressResult.NormalizedValue);
        Assert.Equal("24-CV-98531", identifierResult.NormalizedValue);
        Assert.Equal("24CV98531", identifierResult.ComparisonKey);
        Assert.Equal("A title with spaces", textResult.NormalizedValue);
    }

    [Fact]
    public async Task Service_preserves_raw_evidence_confidence_and_partial_fact_outcomes()
    {
        var fixture = Fixture.Create(
        [
            fixtureFact("PATIENT_NAME", ExtractionFactDataTypes.Name, "John Smith", 0.45, ["Patient: John Smith"], 0),
            fixtureFact("DOCUMENT_DATE", ExtractionFactDataTypes.Date, "02/30/2026", 0.90, ["Date: 02/30/2026"], 1),
            fixtureFact("UNSUPPORTED_FACT", ExtractionFactDataTypes.Text, "kept", 0.80, [], 2),
        ]);

        var result = await fixture.Service.NormalizeAsync(
            fixture.TenantId,
            fixture.Artifact.Id,
            null,
            null,
            "normalization-1",
            CancellationToken.None);

        Assert.Equal(NormalizationRunStatuses.Partial, result.Status);
        Assert.Equal(3, result.Facts.Count);
        var name = Assert.Single(result.Facts, fact => fact.FactCode == "PATIENT_NAME");
        Assert.Equal("John Smith", name.RawValue);
        Assert.Equal(0.45, name.SourceConfidence);
        Assert.Equal("Patient: John Smith", Assert.Single(name.Evidence));
        Assert.Equal(NormalizationStatuses.Normalized, name.NormalizationStatus);
        var invalid = Assert.Single(result.Facts, fact => fact.FactCode == "DOCUMENT_DATE");
        Assert.Equal(NormalizationStatuses.Invalid, invalid.NormalizationStatus);
        var unsupported = Assert.Single(result.Facts, fact => fact.FactCode == "UNSUPPORTED_FACT");
        Assert.Equal(NormalizationStatuses.Unsupported, unsupported.NormalizationStatus);
    }

    [Fact]
    public async Task Service_is_idempotent_and_keeps_history_for_a_new_extraction()
    {
        var fixture = Fixture.Create(
        [
            fixtureFact("PATIENT_NAME", ExtractionFactDataTypes.Name, "Jane Doe", 0.91, [], 0),
        ]);

        var first = await fixture.Service.NormalizeAsync(
            fixture.TenantId, fixture.Artifact.Id, null, null, null, CancellationToken.None);
        var duplicate = await fixture.Service.NormalizeAsync(
            fixture.TenantId, fixture.Artifact.Id, null, null, null, CancellationToken.None);

        fixture.ExtractionRepository.Current = new ArtifactExtraction
        {
            Id = Guid.NewGuid(),
            TenantId = fixture.TenantId,
            IntakeArtifactId = fixture.Artifact.Id,
            Status = ExtractionStatuses.Completed,
            ExecutionKey = "new-extraction",
            AttemptNumber = 1,
            IsCurrent = true,
            Facts =
            [
                new ArtifactExtractedFact
                {
                    Id = Guid.NewGuid(),
                    TenantId = fixture.TenantId,
                    FactCode = "PATIENT_NAME",
                    DataType = ExtractionFactDataTypes.Name,
                    RawValue = "Jane Doe",
                    Confidence = 0.91,
                    EvidenceJson = "[]",
                    FactOrdinal = 0,
                },
            ],
        };
        var second = await fixture.Service.NormalizeAsync(
            fixture.TenantId, fixture.Artifact.Id, null, null, null, CancellationToken.None);

        Assert.Equal(first.Id, duplicate.Id);
        Assert.NotEqual(first.Id, second.Id);
        Assert.Equal(2, fixture.NormalizationRepository.Normalizations.Count);
        Assert.False(fixture.NormalizationRepository.Normalizations.Single(item => item.Id == first.Id).IsCurrent);
        Assert.True(fixture.NormalizationRepository.Normalizations.Single(item => item.Id == second.Id).IsCurrent);
    }

    [Fact]
    public async Task Service_is_tenant_scoped_and_requires_current_completed_extraction()
    {
        var fixture = Fixture.Create();
        var missingTenant = await Assert.ThrowsAsync<IntakeConfigurationException>(() =>
            fixture.Service.NormalizeAsync(
                Guid.NewGuid(), fixture.Artifact.Id, null, null, null, CancellationToken.None));

        fixture.ExtractionRepository.Current!.Status = ExtractionStatuses.Failed;
        var incomplete = await Assert.ThrowsAsync<IntakeConfigurationException>(() =>
            fixture.Service.NormalizeAsync(
                fixture.TenantId, fixture.Artifact.Id, null, null, null, CancellationToken.None));

        Assert.Equal("ARTIFACT_NOT_FOUND", missingTenant.Code);
        Assert.Equal(NormalizationFailureCodes.ExtractionRequired, incomplete.Code);
    }

    private static ArtifactExtractedFact fixtureFact(
        string code,
        string dataType,
        string rawValue,
        double confidence,
        IReadOnlyList<string> evidence,
        int ordinal) =>
        new()
        {
            Id = Guid.NewGuid(),
            FactCode = code,
            DataType = dataType,
            RawValue = rawValue,
            Confidence = confidence,
            EvidenceJson = JsonSerializer.Serialize(evidence),
            FactOrdinal = ordinal,
        };

    private sealed class Fixture
    {
        public Guid TenantId { get; } = Guid.NewGuid();
        public IntakeArtifact Artifact { get; }
        public FakeNormalizationRepository NormalizationRepository { get; }
        public FakeExtractionRepository ExtractionRepository { get; }
        public NormalizationService Service { get; }

        private Fixture(IReadOnlyList<ArtifactExtractedFact> facts)
        {
            Artifact = new IntakeArtifact
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                ArtifactType = IntakeArtifactTypes.Attachment,
                ArtifactRole = IntakeArtifactRoles.Attachment,
                ArtifactSourceType = "MANUAL",
                ArtifactKey = "manual/normalization",
                EffectiveFileName = "document.txt",
                OriginalFileName = "document.txt",
                DeclaredContentType = "text/plain",
                ProcessingStatus = IntakeArtifactProcessingStatuses.Completed,
                Sha256 = new string('a', 64),
            };
            var classification = new ArtifactClassification
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
            ExtractionRepository = new FakeExtractionRepository(new ArtifactExtraction
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                IntakeArtifactId = Artifact.Id,
                ClassificationId = classification.Id,
                ClassificationCode = classification.ClassificationCode!,
                Status = ExtractionStatuses.Completed,
                ExecutionKey = "extraction-1",
                AttemptNumber = 1,
                IsCurrent = true,
                Facts = facts.Select(fact =>
                {
                    fact.TenantId = TenantId;
                    return fact;
                }).ToList(),
            });
            NormalizationRepository = new FakeNormalizationRepository();
            Service = new NormalizationService(
                NormalizationRepository,
                ExtractionRepository,
                new FakeClassificationRepository(TenantId, Artifact, classification),
                new FakeConfigurationService(),
                Registry(),
                new NoopAuditSink(),
                NullLogger<NormalizationService>.Instance);
        }

        public static Fixture Create(IReadOnlyList<ArtifactExtractedFact>? facts = null) =>
            new(facts ?? [fixtureFact("PATIENT_NAME", ExtractionFactDataTypes.Name, "Jane Doe", 0.95, [], 0)]);
    }

    private sealed class FakeNormalizationRepository : IArtifactNormalizationRepository
    {
        public List<ArtifactNormalization> Normalizations { get; } = [];

        public Task<NormalizationProfileDefinition?> FindProfileAsync(
            string code, int? version, CancellationToken cancellationToken) =>
            Task.FromResult<NormalizationProfileDefinition?>(
                code == "LIEN_INTAKE_NORMALIZATION_V1"
                    ? new NormalizationProfileDefinition
                    {
                        Code = code,
                        DisplayName = "Lien Intake Normalization",
                        Version = 1,
                        IsActive = true,
                        NormalizerVersion = "1",
                        UnicodeForm = "NFKC",
                        ComparisonKeyStrategy = "UPPER_ASCII_ALNUM",
                        DefaultDateCulture = "en-US",
                        DefaultCountryCode = "US",
                        DefaultCurrencyCode = "USD",
                        SupportedFactCodesJson = JsonSerializer.Serialize(
                            ExtractionFactCatalog.All.Select(item => new
                            {
                                code = item.Code,
                                dataType = item.DataType,
                            })),
                    }
                    : null);

        public Task<IReadOnlyList<NormalizationProfileDefinition>> ListProfilesAsync(CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<NormalizationProfileDefinition>>([]);

        public Task<ArtifactNormalization?> FindCurrentAsync(
            Guid tenantId, Guid artifactId, Guid extractionId, CancellationToken ct) =>
            Task.FromResult(Normalizations.LastOrDefault(item =>
                item.TenantId == tenantId &&
                item.IntakeArtifactId == artifactId &&
                item.ArtifactExtractionId == extractionId &&
                item.IsCurrent));

        public Task<ArtifactNormalization?> FindByExecutionKeyAsync(
            Guid tenantId, string key, CancellationToken ct) =>
            Task.FromResult(Normalizations.LastOrDefault(item =>
                item.TenantId == tenantId && item.ExecutionKey == key));

        public Task<IReadOnlyList<ArtifactNormalization>> ListHistoryAsync(
            Guid tenantId, Guid artifactId, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<ArtifactNormalization>>(
                Normalizations.Where(item =>
                    item.TenantId == tenantId && item.IntakeArtifactId == artifactId).ToArray());

        public Task<bool> TryAddNormalizationAsync(
            ArtifactNormalization normalization, CancellationToken ct)
        {
            if (Normalizations.Any(item => item.ExecutionKey == normalization.ExecutionKey))
                return Task.FromResult(false);
            Normalizations.Add(normalization);
            return Task.FromResult(true);
        }

        public Task FinalizeCurrentAsync(
            Guid tenantId,
            Guid artifactId,
            ArtifactNormalization normalization,
            IReadOnlyList<ArtifactNormalizedFact> facts,
            CancellationToken ct)
        {
            foreach (var item in Normalizations.Where(item =>
                         item.TenantId == tenantId &&
                         item.IntakeArtifactId == artifactId &&
                         item.Id != normalization.Id))
            {
                item.IsCurrent = false;
                item.CurrentResultMarker = null;
            }
            normalization.Facts = facts.ToList();
            normalization.IsCurrent = true;
            normalization.CurrentResultMarker = "CURRENT";
            return Task.CompletedTask;
        }

        public Task SaveAsync(CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class FakeExtractionRepository(ArtifactExtraction current)
        : IArtifactExtractionRepository
    {
        public ArtifactExtraction? Current { get; set; } = current;

        public Task<ExtractionProfileDefinition?> FindProfileAsync(string code, int? version, CancellationToken ct) =>
            Task.FromResult<ExtractionProfileDefinition?>(null);
        public Task<ExtractionSchemaDefinition?> FindSchemaAsync(string code, int version, string classificationCode, CancellationToken ct) =>
            Task.FromResult<ExtractionSchemaDefinition?>(null);
        public Task<ExtractionPromptDefinition?> FindPromptAsync(string code, int version, string classificationCode, CancellationToken ct) =>
            Task.FromResult<ExtractionPromptDefinition?>(null);
        public Task<IReadOnlyList<ExtractionProfileDefinition>> ListProfilesAsync(CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<ExtractionProfileDefinition>>([]);
        public Task<ArtifactExtraction?> FindCurrentAsync(Guid tenantId, Guid artifactId, Guid classificationId, CancellationToken ct) =>
            Task.FromResult(Current is { TenantId: var currentTenant, IntakeArtifactId: var currentArtifact }
                && currentTenant == tenantId && currentArtifact == artifactId
                ? Current
                : null);
        public Task<ArtifactExtraction?> FindByExecutionKeyAsync(Guid tenantId, string key, CancellationToken ct) =>
            Task.FromResult<ArtifactExtraction?>(null);
        public Task<IReadOnlyList<ArtifactExtraction>> ListHistoryAsync(Guid tenantId, Guid artifactId, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<ArtifactExtraction>>(Current is not null ? [Current] : []);
        public Task<bool> TryClaimAsync(Guid tenantId, Guid extractionId, bool retryFailed, CancellationToken ct) =>
            Task.FromResult(false);
        public Task FinalizeCurrentAsync(Guid tenantId, Guid artifactId, ArtifactExtraction extraction, IReadOnlyList<ArtifactExtractedFact> facts, CancellationToken ct) =>
            Task.CompletedTask;
        public Task<bool> TryAddExtractionAsync(ArtifactExtraction extraction, CancellationToken ct) =>
            Task.FromResult(false);
        public Task SaveAsync(CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class FakeClassificationRepository(
        Guid tenantId,
        IntakeArtifact artifact,
        ArtifactClassification classification) : IClassificationRepository
    {
        public Task<TenantAiPolicy?> FindPolicyAsync(Guid id, CancellationToken ct) =>
            Task.FromResult<TenantAiPolicy?>(null);
        public Task SavePolicyAsync(TenantAiPolicy policy, CancellationToken ct) => Task.CompletedTask;
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
            Task.FromResult<IReadOnlyList<ArtifactClassification>>([]);
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

    private sealed class FakeConfigurationService : IIntakeConfigurationService
    {
        public Task<ResolvedProcessingConfiguration> ResolveAsync(Guid tenantId, string? profileCode, CancellationToken ct) =>
            Task.FromResult(new ResolvedProcessingConfiguration(
                tenantId,
                "LIEN_INTAKE_V1",
                1,
                1,
                1,
                new LienIntakeV1Configuration
                {
                    EnableNormalization = true,
                    NormalizationProfileCode = "LIEN_INTAKE_NORMALIZATION_V1",
                    DefaultCountryCode = "US",
                    DefaultCurrencyCode = "USD",
                    DateCulture = "en-US",
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

    private sealed class NoopAuditSink : INormalizationAuditSink
    {
        public Task RecordAsync(NormalizationAuditEntry entry, CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }
}