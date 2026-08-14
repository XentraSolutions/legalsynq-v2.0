using Intake.Application.Policy;
using Intake.Application.Configuration;
using Intake.Contracts.Configuration;
using Intake.Domain.Artifacts;
using Intake.Domain.Classification;
using Intake.Domain.Extraction;
using Intake.Domain.Matching;
using Intake.Domain.Normalization;
using Intake.Domain.Policy;
using Xunit;

namespace Intake.Tests;

public sealed class PolicyTests
{
    [Fact]
    public void Lien_policy_profile_is_versioned_and_declares_the_full_upstream_chain()
    {
        var profile = PolicyProfileDefaults.Parse(PolicyProfileDefaults.DefinitionJson);

        PolicyProfileDefaults.Validate(profile);
        Assert.Equal(PolicyProfileDefaults.Code, PolicyProfileDefaults.Code);
        Assert.Equal(1, PolicyProfileDefaults.Version);
        Assert.Equal(
            [
                "CLASSIFICATION",
                "EXTRACTION",
                "NORMALIZATION",
                "MATCHING",
            ],
            profile.RequiredUpstreamStages);
        Assert.Contains("LIEN_DOCUMENT", profile.SupportedClassifications);
        Assert.Contains(
            "LIEN_AMOUNT",
            profile.ClassificationPolicies["LIEN_DOCUMENT"].RequiredFacts);
    }

    [Fact]
    public void Policy_profile_validation_rejects_unknown_facts_and_evidence_codes()
    {
        var profile = PolicyProfileDefaults.Parse(PolicyProfileDefaults.DefinitionJson);
        profile.ClassificationPolicies["LIEN_DOCUMENT"].RequiredFacts.Add("NOT_A_FACT");

        var exception = Assert.Throws<InvalidOperationException>(() =>
            PolicyProfileDefaults.Validate(profile));

        Assert.Contains("unknown fact code", exception.Message);
    }

    [Fact]
    public void Required_facts_rule_preserves_a_missing_fact_as_a_blocking_finding()
    {
        var context = Context(
            normalizationFacts:
            [
                NormalizedFact("PATIENT_NAME", "JOHN SMITH"),
            ]);
        var state = new PolicyEvaluationState();

        new RequiredFactsRule().Evaluate(
            new PolicyRuleContext(
                context,
                ConservativeConfiguration(),
                Profile()),
            state);

        Assert.Contains(
            state.Findings,
            finding => finding.ReasonCode == PolicyReasonCodes.RequiredFactMissing &&
                       finding.FactCode == "PROVIDER_NAME" &&
                       finding.Severity == PolicyFindingSeverities.Blocking);
    }

    [Fact]
    public void Low_classification_confidence_triggers_review_without_mutating_source_result()
    {
        var classification = new ArtifactClassification
        {
            Id = Guid.NewGuid(),
            TenantId = TenantId,
            IntakeArtifactId = ArtifactId,
            Status = ClassificationStatuses.Completed,
            ClassificationCode = "LIEN_DOCUMENT",
            Confidence = 0.55,
        };
        var context = Context(classification: classification);
        var state = new PolicyEvaluationState();

        new ClassificationConfidenceRule().Evaluate(
            new PolicyRuleContext(
                context,
                ConservativeConfiguration(),
                Profile()),
            state);

        Assert.Equal(0.55, classification.Confidence);
        Assert.Contains(
            state.Findings,
            finding => finding.ReasonCode == PolicyReasonCodes.ClassificationLowConfidence);
    }

    [Fact]
    public void Required_patient_match_below_threshold_is_no_match()
    {
        var context = Context(
            matchRun: MatchRun(
                EntityMatch(MatchingEntityTypes.Patient, 0.72m),
                EntityMatch(MatchingEntityTypes.Provider, 0.95m)));
        var state = new PolicyEvaluationState();
        var configuration = ConservativeConfiguration();

        new PatientMatchRule().Evaluate(
            new PolicyRuleContext(context, configuration, Profile()),
            state);
        new ProviderFacilityMatchRule().Evaluate(
            new PolicyRuleContext(context, configuration, Profile()),
            state);

        var disposition = PolicyDispositionResolver.Resolve(
            state,
            0.80m,
            configuration,
            Profile());

        Assert.Equal(PolicyDispositionCodes.NoMatch, disposition);
        Assert.Contains(
            state.Findings,
            finding => finding.ReasonCode == PolicyReasonCodes.PatientMatchBelowThreshold);
    }

    [Fact]
    public void Candidate_margin_triggers_an_ambiguity_review()
    {
        var context = Context(
            matchRun: MatchRun(
                EntityMatch(MatchingEntityTypes.Patient, 0.90m, rank: 1),
                EntityMatch(MatchingEntityTypes.Patient, 0.84m, rank: 2)));
        var state = new PolicyEvaluationState();

        new PatientMatchRule().Evaluate(
            new PolicyRuleContext(
                context,
                ConservativeConfiguration(),
                Profile()),
            state);

        Assert.Contains(
            state.Findings,
            finding => finding.ReasonCode == PolicyReasonCodes.PatientMatchAmbiguous &&
                       finding.Severity == PolicyFindingSeverities.Review);
    }

    [Fact]
    public void Exact_duplicate_has_precedence_over_review_and_is_duplicate()
    {
        var context = Context(
            matchRun: MatchRun(new ArtifactDuplicateSignal
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                DuplicateType = DuplicateTypes.ExactArtifactDuplicate,
                Status = DuplicateStatuses.ConfirmedSignal,
                ReasonCode = MatchReasonCodes.ExactArtifactHash,
                Score = 1m,
            }));
        var state = new PolicyEvaluationState();
        var configuration = ConservativeConfiguration();

        new DuplicateRule().Evaluate(
            new PolicyRuleContext(context, configuration, Profile()),
            state);

        Assert.Equal(
            PolicyDispositionCodes.Duplicate,
            PolicyDispositionResolver.Resolve(state, 0.99m, configuration, Profile()));
    }

    [Fact]
    public void Hard_conflict_has_precedence_over_a_normal_review()
    {
        var conflict = EntityMatch(MatchingEntityTypes.Patient, 0.95m);
        conflict.MatchStatus = MatchStatuses.Conflicted;
        conflict.ConflictingFieldCount = 1;
        conflict.Fields.Add(new ArtifactMatchField
        {
            FactCode = "PATIENT_IDENTIFIER",
            MatchOutcome = MatchOutcomes.Conflict,
            ReasonCode = MatchReasonCodes.IdentifierConflict,
        });
        var context = Context(matchRun: MatchRun(conflict));
        var state = new PolicyEvaluationState();
        var configuration = ConservativeConfiguration();

        new HardConflictRule().Evaluate(
            new PolicyRuleContext(context, configuration, Profile()),
            state);

        Assert.Equal(
            PolicyDispositionCodes.Conflicted,
            PolicyDispositionResolver.Resolve(state, 0.90m, configuration, Profile()));
    }

    [Fact]
    public void Missing_required_evidence_is_explainable_and_reduces_confidence()
    {
        var context = Context(
            normalizationFacts:
            [
                NormalizedFact("PATIENT_NAME", "JOHN SMITH", evidence: "[]"),
            ]);
        var state = new PolicyEvaluationState();

        new EvidencePresenceRule().Evaluate(
            new PolicyRuleContext(
                context,
                ConservativeConfiguration(),
                Profile()),
            state);

        var confidence = PolicyConfidenceCalculator.Calculate(state, Profile());
        Assert.Contains(
            state.Findings,
            finding => finding.ReasonCode == PolicyReasonCodes.EvidenceMissing);
        Assert.Equal(0m, state.Components["evidence"]);
        Assert.Equal(0m, confidence);
    }

    [Fact]
    public void Auto_acceptable_is_disabled_by_default_and_review_priority_is_safe()
    {
        var configuration = ConservativeConfiguration();
        var state = new PolicyEvaluationState();

        var disposition = PolicyDispositionResolver.Resolve(
            state,
            0.99m,
            configuration,
            Profile());

        Assert.Equal(PolicyDispositionCodes.ReviewRequired, disposition);
        Assert.Equal(
            PolicyReviewPriorities.Normal,
            PolicyReviewPriorityResolver.Resolve(state, disposition));
    }

    [Fact]
    public void Profile_default_review_cannot_be_overridden_by_tenant_auto_flags()
    {
        var configuration = ConservativeConfiguration();
        configuration.EnableAutoAcceptableDisposition = true;
        configuration.AllowAutoApproval = true;
        var state = new PolicyEvaluationState();

        var disposition = PolicyDispositionResolver.Resolve(
            state,
            0.99m,
            configuration,
            Profile());

        Assert.Equal(PolicyDispositionCodes.ReviewRequired, disposition);
    }

    [Fact]
    public void Disabled_profile_duplicate_rule_does_not_emit_a_duplicate_finding()
    {
        var profile = Profile();
        profile.DuplicatePolicies["EXACT_ARTIFACT"].Enabled = false;
        var context = Context(
            matchRun: MatchRun(new ArtifactDuplicateSignal
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                DuplicateType = DuplicateTypes.ExactArtifactDuplicate,
                Status = DuplicateStatuses.ConfirmedSignal,
                ReasonCode = MatchReasonCodes.ExactArtifactHash,
                Score = 1m,
            }));
        var state = new PolicyEvaluationState();

        new DuplicateRule().Evaluate(
            new PolicyRuleContext(context, ConservativeConfiguration(), profile),
            state);

        Assert.DoesNotContain(
            state.Findings,
            finding => finding.ReasonCode == PolicyReasonCodes.ExactDuplicate);
    }

    [Fact]
    public void Exact_duplicate_profile_review_disposition_is_not_overridden_by_block_flag()
    {
        var profile = Profile();
        profile.DuplicatePolicies["EXACT_ARTIFACT"].Disposition =
            PolicyDispositionCodes.ReviewRequired;
        var context = Context(
            matchRun: MatchRun(new ArtifactDuplicateSignal
            {
                Id = Guid.NewGuid(),
                TenantId = TenantId,
                DuplicateType = DuplicateTypes.ExactArtifactDuplicate,
                Status = DuplicateStatuses.ConfirmedSignal,
                ReasonCode = MatchReasonCodes.ExactArtifactHash,
                Score = 1m,
            }));
        var state = new PolicyEvaluationState();
        var configuration = ConservativeConfiguration();

        new DuplicateRule().Evaluate(
            new PolicyRuleContext(context, configuration, profile),
            state);

        Assert.Equal(
            PolicyDispositionCodes.ReviewRequired,
            PolicyDispositionResolver.Resolve(state, 0.99m, configuration, profile));
    }

    [Fact]
    public void Ordinary_name_conflict_does_not_escalate_to_hard_conflict()
    {
        var match = EntityMatch(MatchingEntityTypes.Patient, 0.70m);
        match.MatchStatus = MatchStatuses.Conflicted;
        match.ConflictingFieldCount = 1;
        match.Fields.Add(new ArtifactMatchField
        {
            FactCode = "PATIENT_NAME",
            MatchOutcome = MatchOutcomes.Conflict,
            ReasonCode = MatchReasonCodes.NameFuzzy,
        });
        var context = Context(matchRun: MatchRun(match));
        var state = new PolicyEvaluationState();

        new HardIdentifierRule().Evaluate(
            new PolicyRuleContext(context, ConservativeConfiguration(), Profile()),
            state);
        new HardConflictRule().Evaluate(
            new PolicyRuleContext(context, ConservativeConfiguration(), Profile()),
            state);

        Assert.DoesNotContain(
            state.Findings,
            finding => finding.ReasonCode is
                PolicyReasonCodes.HardConflict or
                PolicyReasonCodes.HardIdentifierConflict);
    }

    [Fact]
    public void New_policy_configuration_guardrails_reject_out_of_range_values()
    {
        var registry = new Intake.Application.Configuration.ProcessingProfileRegistry();

        var exception = Assert.Throws<IntakeConfigurationException>(() =>
            registry.ValidateAndDeserialize(
                ProcessingProfileCodes.LienIntakeV1,
                """{"minimumPatientMatchMargin":1.2}"""));

        Assert.Equal("INVALID_POLICY_GUARDRAILS", exception.Code);
    }

    private static PolicyProfileDocument Profile() =>
        PolicyProfileDefaults.Parse(PolicyProfileDefaults.DefinitionJson);

    private static LienIntakeV1Configuration ConservativeConfiguration() =>
        new()
        {
            EnablePatientMatching = true,
            EnableFacilityMatching = true,
            RequirePatientMatch = true,
            RequireProviderOrFacilityMatch = true,
            RequireCaseMatch = false,
            EnableAutoAcceptableDisposition = false,
            AllowAutoApproval = false,
            ReviewOnAmbiguousFacts = true,
            ReviewOnHardConflict = true,
            BlockOnExactDuplicate = true,
            ReviewOnPossibleDuplicate = true,
        };

    private static PolicyEvaluationContext Context(
        ArtifactClassification? classification = null,
        ArtifactExtraction? extraction = null,
        ArtifactNormalization? normalization = null,
        ArtifactMatchRun? matchRun = null,
        IReadOnlyList<ArtifactNormalizedFact>? normalizationFacts = null)
    {
        classification ??= new ArtifactClassification
        {
            Id = ClassificationId,
            TenantId = TenantId,
            IntakeArtifactId = ArtifactId,
            Status = ClassificationStatuses.Completed,
            ClassificationCode = "LIEN_DOCUMENT",
            Confidence = 0.95,
        };
        extraction ??= new ArtifactExtraction
        {
            Id = ExtractionId,
            TenantId = TenantId,
            IntakeArtifactId = ArtifactId,
            ClassificationId = classification.Id,
            Status = ExtractionStatuses.Completed,
        };
        normalization ??= new ArtifactNormalization
        {
            Id = NormalizationId,
            TenantId = TenantId,
            IntakeArtifactId = ArtifactId,
            ArtifactExtractionId = extraction.Id,
            Status = NormalizationRunStatuses.Completed,
            Facts = (normalizationFacts ?? []).ToList(),
        };
        matchRun ??= MatchRun();
        return new PolicyEvaluationContext(
            TenantId,
            new IntakeArtifact
            {
                Id = ArtifactId,
                TenantId = TenantId,
            },
            classification,
            extraction,
            normalization,
            matchRun);
    }

    private static ArtifactNormalizedFact NormalizedFact(
        string factCode,
        string value,
        string evidence = """[""page:1""]""") =>
        new()
        {
            Id = Guid.NewGuid(),
            TenantId = TenantId,
            ArtifactNormalizationId = NormalizationId,
            FactCode = factCode,
            DataType = ExtractionFactDataTypes.Text,
            NormalizedValue = value,
            ComparisonKey = value.Replace(" ", string.Empty).ToUpperInvariant(),
            ValidationStatus = ValidationStatuses.Valid,
            SourceConfidence = 0.95,
            EvidenceReferenceJson = evidence,
        };

    private static ArtifactMatchRun MatchRun(
        params object[] values)
    {
        var run = new ArtifactMatchRun
        {
            Id = MatchRunId,
            TenantId = TenantId,
            IntakeArtifactId = ArtifactId,
            ArtifactNormalizationId = NormalizationId,
            Status = MatchRunStatuses.Completed,
        };
        foreach (var value in values)
        {
            switch (value)
            {
                case ArtifactEntityMatch entityMatch:
                    run.EntityMatches.Add(entityMatch);
                    break;
                case ArtifactDuplicateSignal duplicate:
                    run.DuplicateSignals.Add(duplicate);
                    break;
            }
        }
        return run;
    }

    private static ArtifactEntityMatch EntityMatch(
        string entityType,
        decimal score,
        int rank = 1) =>
        new()
        {
            Id = Guid.NewGuid(),
            TenantId = TenantId,
            ArtifactMatchRunId = MatchRunId,
            EntityType = entityType,
            CandidateEntityId = Guid.NewGuid(),
            CandidateDisplayLabel = "candidate",
            Score = score,
            Rank = rank,
            MatchStatus = score >= 0.8m
                ? MatchStatuses.Strong
                : MatchStatuses.Possible,
            IsTopCandidate = rank == 1,
        };

    private static readonly Guid TenantId =
        new("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa");
    private static readonly Guid ArtifactId =
        new("bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb");
    private static readonly Guid ClassificationId =
        new("cccccccc-cccc-4ccc-8ccc-cccccccccccc");
    private static readonly Guid ExtractionId =
        new("dddddddd-dddd-4ddd-8ddd-dddddddddddd");
    private static readonly Guid NormalizationId =
        new("eeeeeeee-eeee-4eee-8eee-eeeeeeeeeeee");
    private static readonly Guid MatchRunId =
        new("ffffffff-ffff-4fff-8fff-ffffffffffff");
}