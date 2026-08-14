using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Intake.Application.Artifacts;
using Intake.Application.Classification;
using Intake.Application.Configuration;
using Intake.Application.Extraction;
using Intake.Application.Matching;
using Intake.Application.Normalization;
using Intake.Contracts.Configuration;
using Intake.Contracts.Matching;
using Intake.Domain.Artifacts;
using Intake.Domain.Classification;
using Intake.Domain.Extraction;
using Intake.Domain.Matching;
using Intake.Domain.Normalization;
using Intake.Infrastructure.Matching;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Intake.Tests;

public sealed class MatchingTests
{
    [Fact]
    public void Matching_profile_has_explicit_entities_weights_and_scoring_version()
    {
        var profile = new MatchingProfileDefinition
        {
            Code = MatchingProfileDefaults.Code,
            Version = MatchingProfileDefaults.Version,
            ScoringVersion = MatchingProfileDefaults.ScoringVersion,
            DefinitionJson = MatchingProfileDefaults.DefinitionJson,
        };

        var document = MatchingProfileParser.Parse(profile);

        Assert.Equal(
            ["PATIENT", "PROVIDER", "FACILITY", "ATTORNEY", "LAW_FIRM", "CASE"],
            document.EntityTypes);
        Assert.Equal(0.30m, document.EntityRules["PATIENT"].Fields
            .Single(field => field.FactCode == "DATE_OF_BIRTH").Weight);
        Assert.True(document.EntityRules["PATIENT"].StrongRequiresHardIdentifier);
        Assert.Equal(
            "PATIENT_PROVIDER_ACCOUNT_SERVICE_DATE",
            document.PrimaryDuplicateRule!.Code);
    }

    [Fact]
    public void Exact_and_fuzzy_name_scores_reuse_comparison_keys()
    {
        var rule = new MatchingEntityRule(
            [new("PATIENT_NAME", "PATIENT_NAME", "PERSON_NAME", 1m, 0m, false)],
            0.80m,
            0.50m,
            1,
            false,
            0.49m);
        var source = Fact("PATIENT_NAME", "John Smith", "JOHNSMITH");

        var exact = MatchScoring.Score(
            MatchingEntityTypes.Patient,
            rule,
            Candidate("John", ("PATIENT_NAME", "John Smith", "JOHNSMITH")),
            [source],
            useSourceConfidence: false);
        var fuzzy = MatchScoring.Score(
            MatchingEntityTypes.Patient,
            rule,
            Candidate("Jon", ("PATIENT_NAME", "Jon Smith", "JONSMITH")),
            [source],
            useSourceConfidence: false);

        Assert.Equal(1m, exact.Score);
        Assert.Equal(MatchOutcomes.NormalizedExact, Assert.Single(exact.Fields).MatchOutcome);
        Assert.Equal(MatchStatuses.Strong, exact.Status);
        Assert.InRange(fuzzy.Score, 0.80m, 1m);
        Assert.Equal(MatchOutcomes.Fuzzy, Assert.Single(fuzzy.Fields).MatchOutcome);
    }

    [Fact]
    public void Conflicts_are_penalized_and_hard_conflicts_cap_status()
    {
        var rule = new MatchingEntityRule(
            [
                new("PATIENT_NAME", "PATIENT_NAME", "PERSON_NAME", 0.50m, 0m, false),
                new("DATE_OF_BIRTH", "DATE_OF_BIRTH", "EXACT", 0.50m, 0.50m, true),
            ],
            0.80m,
            0.20m,
            2,
            true,
            0.49m);
        var result = MatchScoring.Score(
            MatchingEntityTypes.Patient,
            rule,
            Candidate(
                "Different DOB",
                ("PATIENT_NAME", "John Smith", "JOHNSMITH"),
                ("DATE_OF_BIRTH", "1990-01-01", "19900101")),
            [
                Fact("PATIENT_NAME", "John Smith", "JOHNSMITH"),
                Fact("DATE_OF_BIRTH", "1980-01-01", "19800101"),
            ],
            useSourceConfidence: false);

        Assert.Equal(MatchStatuses.Conflicted, result.Status);
        Assert.True(result.ConflictingFieldCount == 1);
        Assert.True(result.Score <= 0.49m);
        Assert.Contains(result.Fields, field => field.ReasonCode == MatchReasonCodes.DobConflict);
    }

    [Fact]
    public void Source_confidence_can_be_disabled_or_reduce_effective_weight()
    {
        var rule = new MatchingEntityRule(
            [new("PATIENT_NAME", "PATIENT_NAME", "PERSON_NAME", 1m, 0m, false)],
            0.80m,
            0.20m,
            1,
            false,
            0.49m);
        var source = Fact("PATIENT_NAME", "John Smith", "JOHNSMITH", confidence: 0.25);
        var candidate = Candidate("John Smith", ("PATIENT_NAME", "John Smith", "JOHNSMITH"));

        var enabled = MatchScoring.Score(
            MatchingEntityTypes.Patient, rule, candidate, [source], true);
        var disabled = MatchScoring.Score(
            MatchingEntityTypes.Patient, rule, candidate, [source], false);

        Assert.Equal(0.25m, enabled.Fields.Single().EffectiveWeight);
        Assert.Equal(1m, disabled.Fields.Single().EffectiveWeight);
        Assert.Equal(1m, enabled.Score);
        Assert.Equal(1m, disabled.Score);
    }

    [Fact]
    public void Repeated_alias_facts_choose_the_best_compatible_source_deterministically()
    {
        var rule = new MatchingEntityRule(
            [new("PATIENT_NAME", "PATIENT_NAME", "PERSON_NAME", 1m, 0m, false)],
            0.80m,
            0.20m,
            1,
            false,
            0.49m);
        var exactFact = Fact("PATIENT_FULL_NAME", "John Smith", "JOHNSMITH");
        var otherFact = Fact("PATIENT_NAME", "Jane Smith", "JANESMITH");
        var result = MatchScoring.Score(
            MatchingEntityTypes.Patient,
            rule,
            Candidate("John Smith", ("PATIENT_NAME", "John Smith", "JOHNSMITH")),
            [otherFact, exactFact],
            false);

        Assert.Equal(1m, result.Score);
        Assert.Equal(exactFact.SourceNormalizedFactId, Assert.Single(result.Fields).SourceNormalizedFactId);
    }

    [Fact]
    public void Invalid_and_missing_source_values_do_not_contribute()
    {
        var rule = new MatchingEntityRule(
            [
                new("PATIENT_NAME", "PATIENT_NAME", "PERSON_NAME", 0.50m, 0m, false),
                new("DATE_OF_BIRTH", "DATE_OF_BIRTH", "EXACT", 0.50m, 0m, false),
            ],
            0.80m,
            0.20m,
            2,
            false,
            0.49m);
        var result = MatchScoring.Score(
            MatchingEntityTypes.Patient,
            rule,
            Candidate("John Smith", ("PATIENT_NAME", "John Smith", "JOHNSMITH")),
            [
                Fact("PATIENT_NAME", "John Smith", "JOHNSMITH"),
                Fact("DATE_OF_BIRTH", "not-a-date", null, validation: "INVALID_FORMAT"),
            ],
            false);

        Assert.Equal(1m, result.Score);
        Assert.Equal(1, result.MatchedFieldCount);
        Assert.Contains(result.Fields, field =>
            field.ReasonCode == MatchReasonCodes.SourceValueInvalid &&
            field.EffectiveWeight == 0m);
    }

    [Fact]
    public async Task Provider_registry_is_tenant_guarded_and_exposes_initial_entity_types()
    {
        var provider = new TenantMatchCandidateProvider(
            new StaticCandidateSource(CandidateProviderResult.Success([])),
            MatchingEntityTypes.Patient);
        var registry = new MatchCandidateProviderRegistry([provider]);

        Assert.Same(provider, registry.Find(MatchingEntityTypes.Patient));
        var result = await provider.SearchAsync(
            Guid.Empty,
            [],
            50,
            CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal(MatchingFailureCodes.TenantContextInvalid, result.FailureCode);
    }

    [Fact]
    public async Task Http_candidate_source_requires_internal_auth_and_tenant_attestation()
    {
        var tenantId = Guid.NewGuid();
        HttpRequestMessage? request = null;
        using var client = new HttpClient(new StubHttpHandler(message =>
        {
            request = message;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new
                {
                    tenantId,
                    items = new[]
                    {
                        new
                        {
                            tenantId,
                            entityId = Guid.NewGuid(),
                            displayLabel = "John Smith",
                            fields = new Dictionary<string, object>
                            {
                                ["PATIENT_NAME"] = new
                                {
                                    value = "John Smith",
                                    comparisonKey = "JOHNSMITH",
                                    dataType = "PERSON_NAME",
                                },
                            },
                        },
                    },
                }),
            };
        }));
        var source = new HttpTenantMatchCandidateSource(
            client,
            Options.Create(new TenantMatchCandidateOptions
            {
                BaseUrl = "https://candidate-source.example",
                InternalToken = "configured-internal-token",
            }),
            NullLogger<HttpTenantMatchCandidateSource>.Instance);

        var result = await source.SearchAsync(
            tenantId,
            MatchingEntityTypes.Patient,
            [],
            10,
            CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal("configured-internal-token", request!.Headers.GetValues("X-Internal-Token").Single());
        Assert.Equal(tenantId.ToString(), request.Headers.GetValues("X-Tenant-Id").Single());
        Assert.Single(result.Candidates);
    }

    [Fact]
    public async Task Matching_is_idempotent_and_persists_ranked_explainable_results()
    {
        var fixture = MatchingFixture.Create(
            [
                FactModel("PATIENT_NAME", "John Smith", "JOHNSMITH"),
                FactModel("DATE_OF_BIRTH", "1980-01-01", "19800101"),
            ],
            [
                new TenantMatchCandidate(
                    Guid.NewGuid(),
                    "John Smith",
                    Fields(
                        ("PATIENT_NAME", "John Smith", "JOHNSMITH"),
                        ("DATE_OF_BIRTH", "1980-01-01", "19800101"))),
            ]);

        var first = await fixture.Service.MatchAsync(
            fixture.TenantId,
            fixture.Artifact.Id,
            null,
            null,
            "matching-test",
            CancellationToken.None);
        var second = await fixture.Service.MatchAsync(
            fixture.TenantId,
            fixture.Artifact.Id,
            null,
            null,
            "matching-test",
            CancellationToken.None);

        Assert.Equal(first.Id, second.Id);
        Assert.Single(fixture.Repository.Runs);
        var entity = Assert.Single(first.EntityMatches);
        Assert.True(entity.IsTopCandidate);
        Assert.Equal(MatchStatuses.Strong, entity.MatchStatus);
        Assert.Equal(2, entity.FieldBreakdown.Count);
        Assert.Equal(fixture.Normalization.Id, first.ArtifactNormalizationId);
    }

    [Fact]
    public async Task Provider_failure_preserves_successful_results_as_partial()
    {
        var fixture = MatchingFixture.Create(
            [
                FactModel("PATIENT_NAME", "John Smith", "JOHNSMITH"),
                FactModel("PROVIDER_NAME", "Sunrise Clinic", "SUNRISECLINIC"),
            ],
            [
                Candidate(
                    "John Smith",
                    ("PATIENT_NAME", "John Smith", "JOHNSMITH")),
            ],
            new StaticProvider(
                MatchingEntityTypes.Provider,
                CandidateProviderResult.Failure(
                    MatchingFailureCodes.EntityProviderUnavailable,
                    "unavailable")));

        var result = await fixture.Service.MatchAsync(
            fixture.TenantId,
            fixture.Artifact.Id,
            null,
            null,
            null,
            CancellationToken.None);

        Assert.Equal(MatchRunStatuses.Partial, result.Status);
        Assert.Contains(result.EntityMatches, item => item.EntityType == MatchingEntityTypes.Patient);
        Assert.Equal(MatchingFailureCodes.EntityProviderUnavailable, result.FailureCode);
    }

    [Fact]
    public async Task Exact_artifact_duplicates_are_tenant_scoped_and_cross_source_signaled()
    {
        var fixture = MatchingFixture.Create(
            [FactModel("PATIENT_NAME", "John Smith", "JOHNSMITH")],
            [Candidate("John Smith", ("PATIENT_NAME", "John Smith", "JOHNSMITH"))]);
        fixture.Artifact.ArtifactSourceType = "EMAIL";
        fixture.ArtifactRepository.Artifacts.Add(new IntakeArtifact
        {
            Id = Guid.NewGuid(),
            TenantId = fixture.TenantId,
            Sha256 = fixture.Artifact.Sha256,
            ArtifactSourceType = "MANUAL",
        });
        fixture.ArtifactRepository.Artifacts.Add(new IntakeArtifact
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            Sha256 = fixture.Artifact.Sha256,
            ArtifactSourceType = "MANUAL",
        });

        var result = await fixture.Service.MatchAsync(
            fixture.TenantId,
            fixture.Artifact.Id,
            null,
            null,
            null,
            CancellationToken.None);

        Assert.Contains(result.DuplicateSignals, signal =>
            signal.DuplicateType == DuplicateTypes.ExactArtifactDuplicate);
        Assert.Contains(result.DuplicateSignals, signal =>
            signal.DuplicateType == DuplicateTypes.ContentDuplicate);
        Assert.DoesNotContain(result.DuplicateSignals, signal =>
            signal.RelatedArtifactId == fixture.ArtifactRepository.Artifacts[2].Id);
    }

    private static MatchDiscoveryFact Fact(
        string code,
        string value,
        string? comparisonKey,
        double confidence = 0.90,
        string validation = "VALID") =>
        new(Guid.NewGuid(), code, value, comparisonKey, validation, confidence);

    private static ArtifactNormalizedFact FactModel(
        string code,
        string value,
        string comparisonKey) =>
        new()
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            FactCode = code,
            DataType = "TEXT",
            RawValue = value,
            NormalizedValue = value,
            ComparisonKey = comparisonKey,
            NormalizationStatus = NormalizationStatuses.Normalized,
            ValidationStatus = ValidationStatuses.Valid,
            SourceConfidence = 0.95,
        };

    private static TenantMatchCandidate Candidate(
        string label,
        params (string Name, string Value, string Key)[] fields) =>
        new(Guid.NewGuid(), label, Fields(fields));

    private static IReadOnlyDictionary<string, TenantMatchCandidateField> Fields(
        params (string Name, string Value, string Key)[] fields) =>
        fields.ToDictionary(
            field => field.Name,
            field => new TenantMatchCandidateField(field.Value, field.Key),
            StringComparer.OrdinalIgnoreCase);

    private sealed class MatchingFixture
    {
        private MatchingFixture(
            IReadOnlyList<ArtifactNormalizedFact> facts,
            IReadOnlyList<TenantMatchCandidate> patientCandidates,
            params StaticProvider[] extraProviders)
        {
            TenantId = Guid.NewGuid();
            Artifact = new IntakeArtifact
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                ArtifactSourceType = "MANUAL",
                ArtifactType = "ATTACHMENT",
                ArtifactRole = "ATTACHMENT",
                Sha256 = new string('b', 64),
            };
            Classification = new ArtifactClassification
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                IntakeArtifactId = Artifact.Id,
                Status = ClassificationStatuses.Completed,
                IsCurrent = true,
            };
            Extraction = new ArtifactExtraction
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                IntakeArtifactId = Artifact.Id,
                ClassificationId = Classification.Id,
                Status = ExtractionStatuses.Completed,
                IsCurrent = true,
                Facts = [],
            };
            Normalization = new ArtifactNormalization
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                IntakeArtifactId = Artifact.Id,
                ArtifactExtractionId = Extraction.Id,
                ExecutionKey = "normalization-execution",
                Status = NormalizationRunStatuses.Completed,
                IsCurrent = true,
                Facts = facts.Select(fact =>
                {
                    fact.TenantId = TenantId;
                    fact.ArtifactNormalizationId = NormalizationId;
                    return fact;
                }).ToList(),
            };
            NormalizationId = Normalization.Id;
            foreach (var fact in Normalization.Facts)
                fact.ArtifactNormalizationId = Normalization.Id;

            Repository = new FakeMatchingRepository();
            Providers =
            [
                new StaticProvider(
                    MatchingEntityTypes.Patient,
                    CandidateProviderResult.Success(patientCandidates)),
                new StaticProvider(
                    MatchingEntityTypes.Provider,
                    CandidateProviderResult.Success([])),
                ..extraProviders,
            ];
            ArtifactRepository = new FakeArtifactRepository(Artifact);
            Service = new MatchingService(
                Repository,
                new FakeNormalizationRepository(Normalization),
                new FakeExtractionRepository(Extraction),
                new FakeClassificationRepository(Artifact, Classification),
                ArtifactRepository,
                new FakeConfigurationService(),
                new MatchCandidateProviderRegistry(Providers),
                new NoopMatchingAuditSink(),
                NullLogger<MatchingService>.Instance);
        }

        private Guid NormalizationId { get; } = Guid.NewGuid();
        public Guid TenantId { get; }
        public IntakeArtifact Artifact { get; }
        public ArtifactClassification Classification { get; }
        public ArtifactExtraction Extraction { get; }
        public ArtifactNormalization Normalization { get; }
        public FakeMatchingRepository Repository { get; }
        public FakeArtifactRepository ArtifactRepository { get; }
        public MatchingService Service { get; }
        private StaticProvider[] Providers { get; }

        public static MatchingFixture Create(
            IReadOnlyList<ArtifactNormalizedFact> facts,
            IReadOnlyList<TenantMatchCandidate> patientCandidates,
            params StaticProvider[] extraProviders) =>
            new(facts, patientCandidates, extraProviders);
    }

    private sealed class StaticCandidateSource(CandidateProviderResult result)
        : ITenantMatchCandidateSource
    {
        public Task<CandidateProviderResult> SearchAsync(
            Guid tenantId,
            string entityType,
            IReadOnlyList<MatchDiscoveryFact> facts,
            int maxCandidateSearchPool,
            CancellationToken cancellationToken) =>
            Task.FromResult(result);
    }

    private sealed class StubHttpHandler(
        Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(responder(request));
    }

    private sealed class StaticProvider(
        string entityType,
        CandidateProviderResult result) : ITenantMatchCandidateProvider
    {
        public string EntityType { get; } = entityType;

        public Task<CandidateProviderResult> SearchAsync(
            Guid tenantId,
            IReadOnlyList<MatchDiscoveryFact> facts,
            int maxCandidateSearchPool,
            CancellationToken cancellationToken) =>
            Task.FromResult(result);
    }

    private sealed class NoopMatchingAuditSink : IMatchingAuditSink
    {
        public Task RecordAsync(MatchingAuditEntry entry, CancellationToken cancellationToken) =>
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
                "LIEN_INTAKE_V1",
                1,
                1,
                1,
                new LienIntakeV1Configuration
                {
                    EnableMatching = true,
                    MatchingProfileCode = MatchingProfileDefaults.Code,
                    MaxCandidatesPerEntityType = 10,
                    MaxCandidateSearchPool = 50,
                    MinimumCandidateScore = 0.20,
                    EnableDuplicateDetection = true,
                    UseSourceConfidenceInScoring = true,
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

    private sealed class FakeMatchingRepository : IArtifactMatchingRepository
    {
        public List<ArtifactMatchRun> Runs { get; } = [];

        public Task<MatchingProfileDefinition?> FindProfileAsync(string code, int? version, CancellationToken ct) =>
            Task.FromResult<MatchingProfileDefinition?>(
                code == MatchingProfileDefaults.Code
                    ? new MatchingProfileDefinition
                    {
                        Id = MatchingDefinitionIds.LienIntakeMatchingProfileV1,
                        Code = MatchingProfileDefaults.Code,
                        DisplayName = "Matching",
                        Version = MatchingProfileDefaults.Version,
                        ScoringVersion = MatchingProfileDefaults.ScoringVersion,
                        IsActive = true,
                        DefinitionJson = MatchingProfileDefaults.DefinitionJson,
                    }
                    : null);

        public Task<IReadOnlyList<MatchingProfileDefinition>> ListProfilesAsync(CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<MatchingProfileDefinition>>([]);

        public Task<ArtifactMatchRun?> FindCurrentAsync(Guid tenantId, Guid artifactId, Guid normalizationId, CancellationToken ct) =>
            Task.FromResult(Runs.LastOrDefault(run =>
                run.TenantId == tenantId &&
                run.IntakeArtifactId == artifactId &&
                run.ArtifactNormalizationId == normalizationId &&
                run.IsCurrent));

        public Task<ArtifactMatchRun?> FindByExecutionKeyAsync(Guid tenantId, string key, CancellationToken ct) =>
            Task.FromResult(Runs.LastOrDefault(run => run.TenantId == tenantId && run.ExecutionKey == key));

        public Task<IReadOnlyList<ArtifactMatchRun>> ListHistoryAsync(Guid tenantId, Guid artifactId, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<ArtifactMatchRun>>(
                Runs.Where(run => run.TenantId == tenantId && run.IntakeArtifactId == artifactId).ToArray());

        public Task<ArtifactMatchRun?> FindBusinessDuplicateRunAsync(Guid tenantId, string fingerprint, Guid excludedArtifactId, CancellationToken ct) =>
            Task.FromResult(Runs.LastOrDefault(run =>
                run.TenantId == tenantId &&
                run.IntakeArtifactId != excludedArtifactId &&
                run.BusinessKeyFingerprint == fingerprint &&
                run.Status is MatchRunStatuses.Completed or MatchRunStatuses.Partial));

        public Task<bool> TryAddMatchRunAsync(ArtifactMatchRun run, CancellationToken ct)
        {
            if (Runs.Any(item => item.ExecutionKey == run.ExecutionKey))
                return Task.FromResult(false);
            Runs.Add(run);
            return Task.FromResult(true);
        }

        public Task FinalizeCurrentAsync(
            Guid tenantId,
            Guid artifactId,
            ArtifactMatchRun run,
            IReadOnlyList<ArtifactEntityMatch> entityMatches,
            IReadOnlyList<ArtifactMatchField> fields,
            IReadOnlyList<ArtifactDuplicateSignal> duplicateSignals,
            CancellationToken ct)
        {
            foreach (var item in Runs.Where(item =>
                         item.TenantId == tenantId &&
                         item.IntakeArtifactId == artifactId &&
                         item.Id != run.Id))
                item.IsCurrent = false;
            run.IsCurrent = true;
            run.CurrentResultMarker = "CURRENT";
            run.EntityMatches = entityMatches.ToList();
            foreach (var field in fields)
            {
                var match = run.EntityMatches.Single(item => item.Id == field.ArtifactEntityMatchId);
                match.Fields.Add(field);
            }
            run.DuplicateSignals = duplicateSignals.ToList();
            return Task.CompletedTask;
        }

        public Task SaveAsync(CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class FakeNormalizationRepository(ArtifactNormalization current)
        : IArtifactNormalizationRepository
    {
        public Task<NormalizationProfileDefinition?> FindProfileAsync(string code, int? version, CancellationToken ct) =>
            Task.FromResult<NormalizationProfileDefinition?>(null);
        public Task<IReadOnlyList<NormalizationProfileDefinition>> ListProfilesAsync(CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<NormalizationProfileDefinition>>([]);
        public Task<ArtifactNormalization?> FindCurrentAsync(Guid tenantId, Guid artifactId, Guid extractionId, CancellationToken ct) =>
            Task.FromResult<ArtifactNormalization?>(
                current.TenantId == tenantId && current.IntakeArtifactId == artifactId &&
                current.ArtifactExtractionId == extractionId && current.IsCurrent ? current : null);
        public Task<ArtifactNormalization?> FindByExecutionKeyAsync(Guid tenantId, string key, CancellationToken ct) =>
            Task.FromResult<ArtifactNormalization?>(null);
        public Task<IReadOnlyList<ArtifactNormalization>> ListHistoryAsync(Guid tenantId, Guid artifactId, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<ArtifactNormalization>>([current]);
        public Task<bool> TryAddNormalizationAsync(ArtifactNormalization normalization, CancellationToken ct) => Task.FromResult(false);
        public Task FinalizeCurrentAsync(Guid tenantId, Guid artifactId, ArtifactNormalization normalization, IReadOnlyList<ArtifactNormalizedFact> facts, CancellationToken ct) => Task.CompletedTask;
        public Task SaveAsync(CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class FakeExtractionRepository(ArtifactExtraction current)
        : IArtifactExtractionRepository
    {
        public Task<ArtifactExtraction?> FindCurrentAsync(Guid tenantId, Guid artifactId, Guid classificationId, CancellationToken ct) =>
            Task.FromResult<ArtifactExtraction?>(
                current.TenantId == tenantId && current.IntakeArtifactId == artifactId &&
                current.ClassificationId == classificationId && current.IsCurrent ? current : null);
        public Task<ExtractionProfileDefinition?> FindProfileAsync(string code, int? version, CancellationToken ct) => Task.FromResult<ExtractionProfileDefinition?>(null);
        public Task<ExtractionSchemaDefinition?> FindSchemaAsync(string code, int version, string classificationCode, CancellationToken ct) => Task.FromResult<ExtractionSchemaDefinition?>(null);
        public Task<ExtractionPromptDefinition?> FindPromptAsync(string code, int version, string classificationCode, CancellationToken ct) => Task.FromResult<ExtractionPromptDefinition?>(null);
        public Task<IReadOnlyList<ExtractionProfileDefinition>> ListProfilesAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<ExtractionProfileDefinition>>([]);
        public Task<ArtifactExtraction?> FindByExecutionKeyAsync(Guid tenantId, string key, CancellationToken ct) => Task.FromResult<ArtifactExtraction?>(null);
        public Task<IReadOnlyList<ArtifactExtraction>> ListHistoryAsync(Guid tenantId, Guid artifactId, CancellationToken ct) => Task.FromResult<IReadOnlyList<ArtifactExtraction>>([current]);
        public Task<bool> TryClaimAsync(Guid tenantId, Guid extractionId, bool retryFailed, CancellationToken ct) => Task.FromResult(false);
        public Task FinalizeCurrentAsync(Guid tenantId, Guid artifactId, ArtifactExtraction extraction, IReadOnlyList<ArtifactExtractedFact> facts, CancellationToken ct) => Task.CompletedTask;
        public Task<bool> TryAddExtractionAsync(ArtifactExtraction extraction, CancellationToken ct) => Task.FromResult(false);
        public Task SaveAsync(CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class FakeClassificationRepository(
        IntakeArtifact artifact,
        ArtifactClassification classification) : IClassificationRepository
    {
        public Task<IntakeArtifact?> FindArtifactAsync(Guid tenantId, Guid artifactId, CancellationToken ct) =>
            Task.FromResult<IntakeArtifact?>(
                artifact.TenantId == tenantId && artifact.Id == artifactId ? artifact : null);
        public Task<ArtifactClassification?> FindCurrentAsync(Guid tenantId, Guid artifactId, CancellationToken ct) =>
            Task.FromResult<ArtifactClassification?>(
                classification.TenantId == tenantId && classification.IntakeArtifactId == artifactId &&
                classification.IsCurrent ? classification : null);
        public Task<TenantAiPolicy?> FindPolicyAsync(Guid tenantId, CancellationToken ct) => Task.FromResult<TenantAiPolicy?>(null);
        public Task SavePolicyAsync(TenantAiPolicy policy, CancellationToken ct) => Task.CompletedTask;
        public Task<ClassificationProfileDefinition?> FindProfileAsync(string code, int? version, CancellationToken ct) => Task.FromResult<ClassificationProfileDefinition?>(null);
        public Task<ClassificationTaxonomyDefinition?> FindTaxonomyAsync(string code, int version, CancellationToken ct) => Task.FromResult<ClassificationTaxonomyDefinition?>(null);
        public Task<ClassificationPromptDefinition?> FindPromptAsync(string code, int version, CancellationToken ct) => Task.FromResult<ClassificationPromptDefinition?>(null);
        public Task<IReadOnlyList<ClassificationProfileDefinition>> ListProfilesAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<ClassificationProfileDefinition>>([]);
        public Task<ArtifactClassification?> FindByExecutionKeyAsync(Guid tenantId, string key, CancellationToken ct) => Task.FromResult<ArtifactClassification?>(null);
        public Task<IReadOnlyList<ArtifactClassification>> ListHistoryAsync(Guid tenantId, Guid artifactId, CancellationToken ct) => Task.FromResult<IReadOnlyList<ArtifactClassification>>([classification]);
        public Task<bool> TryClaimAsync(Guid tenantId, Guid classificationId, bool retryFailed, CancellationToken ct) => Task.FromResult(false);
        public Task ClearCurrentAsync(Guid tenantId, Guid artifactId, Guid replacementId, CancellationToken ct) => Task.CompletedTask;
        public Task FinalizeCurrentAsync(Guid tenantId, Guid artifactId, ArtifactClassification value, CancellationToken ct) => Task.CompletedTask;
        public Task<bool> TryAddClassificationAsync(ArtifactClassification value, CancellationToken ct) => Task.FromResult(false);
        public Task SaveAsync(CancellationToken ct) => Task.CompletedTask;
    }

    private sealed class FakeArtifactRepository(IntakeArtifact current) : IIntakeArtifactRepository
    {
        public List<IntakeArtifact> Artifacts { get; } = [current];
        public Task<IntakeArtifact?> FindAsync(Guid tenantId, Guid artifactId, CancellationToken ct) =>
            Task.FromResult<IntakeArtifact?>(Artifacts.SingleOrDefault(item => item.TenantId == tenantId && item.Id == artifactId));
        public Task<IReadOnlyList<IntakeArtifact>> ListBySha256Async(Guid tenantId, string sha256, Guid excludedArtifactId, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<IntakeArtifact>>(Artifacts.Where(item => item.TenantId == tenantId && item.Id != excludedArtifactId && item.Sha256 == sha256).ToArray());
        public Task<IReadOnlyList<IntakeArtifact>> ListByEmailAsync(Guid tenantId, Guid emailId, CancellationToken ct) => Task.FromResult<IReadOnlyList<IntakeArtifact>>([]);
        public Task<IReadOnlyList<IntakeArtifact>> ListByManualSubmissionAsync(Guid tenantId, Guid submissionId, CancellationToken ct) => Task.FromResult<IReadOnlyList<IntakeArtifact>>([]);
        public Task<IntakeArtifact?> FindByManualKeyAsync(Guid tenantId, Guid submissionId, string artifactKey, CancellationToken ct) => Task.FromResult<IntakeArtifact?>(null);
        public Task<IntakeArtifact?> FindByKeyAsync(Guid tenantId, Guid emailId, string artifactKey, CancellationToken ct) => Task.FromResult<IntakeArtifact?>(null);
        public Task<IntakeArtifact> AddOrGetAsync(IntakeArtifact artifact, CancellationToken ct) => Task.FromResult(artifact);
        public Task<bool> TryClaimAsync(Guid tenantId, Guid artifactId, bool retryFailed, CancellationToken ct) => Task.FromResult(false);
        public Task SaveAsync(CancellationToken ct) => Task.CompletedTask;
        public Task UpdateManualSubmissionStatusAsync(Guid tenantId, Guid submissionId, string status, string? failureMessage, DateTimeOffset? completedAt, CancellationToken ct) => Task.CompletedTask;
        public Task UpdateEmailProcessingStatusAsync(Guid tenantId, Guid emailId, string status, CancellationToken ct) => Task.CompletedTask;
        public Task<IntakeArtifactAnalyticsResponse> GetAnalyticsAsync(Guid tenantId, Guid? emailId, CancellationToken ct) => throw new NotImplementedException();
    }
}