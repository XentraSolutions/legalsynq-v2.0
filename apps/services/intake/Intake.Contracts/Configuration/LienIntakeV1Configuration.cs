namespace Intake.Contracts.Configuration;

public sealed class LienIntakeV1Configuration
{
    public bool RequireHumanReview { get; set; } = true;
    public bool AllowAutoApproval { get; set; }

    public double AutoApproveThreshold { get; set; } = 0.95;
    public double ReviewThreshold { get; set; } = 0.75;
    public double RejectThreshold { get; set; } = 0.50;

    public bool EnablePatientMatching { get; set; }
    public bool EnableCaseMatching { get; set; }
    public bool EnableFacilityMatching { get; set; }
    public bool EnableDuplicateDetection { get; set; }

    public bool ProcessAttachments { get; set; } = true;
    public bool ProcessEmailBody { get; set; } = true;
    public bool AllowUnsupportedDocuments { get; set; }

    public bool EnableClassification { get; set; } = true;
    public string ClassificationProfileCode { get; set; } = "LIEN_DOCUMENT_CLASSIFICATION_V1";
    public double MinimumClassificationConfidence { get; set; } = 0.80;
    public bool AllowAutoClassification { get; set; }
    public int MaxClassificationInputCharacters { get; set; } = 32_000;
    public int MaxClassificationInputTokens { get; set; } = 8_000;
    public int MaxClassificationOutputTokens { get; set; } = 600;
    public int ClassificationTimeoutSeconds { get; set; } = 60;
    public int ClassificationMaxAttempts { get; set; } = 3;

    public bool EnableExtraction { get; set; } = true;
    public string ExtractionProfileCode { get; set; } = "LIEN_INTAKE_EXTRACTION_V1";
    public double MinimumFactConfidence { get; set; } = 0.70;
    public bool AllowAutomaticExtraction { get; set; }
    public int MaxExtractionInputCharacters { get; set; } = 32_000;
    public int MaxExtractionOutputTokens { get; set; } = 1_200;
    public int ExtractionTimeoutSeconds { get; set; } = 60;
    public int ExtractionMaxAttempts { get; set; } = 3;
    public int MaxExtractedFacts { get; set; } = 100;
    public int MaxFactValueCharacters { get; set; } = 500;
    public int MaxFactEvidenceCharacters { get; set; } = 240;

    public bool EnableNormalization { get; set; } = true;
    public string NormalizationProfileCode { get; set; } = "LIEN_INTAKE_NORMALIZATION_V1";
    public string DefaultCountryCode { get; set; } = "US";
    public string DefaultCurrencyCode { get; set; } = "USD";
    public string DateCulture { get; set; } = "en-US";
    public bool AllowAmbiguousDateNormalization { get; set; }

    public bool EnableMatching { get; set; } = true;
    public string MatchingProfileCode { get; set; } = "LIEN_INTAKE_MATCHING_V1";
    public int MaxCandidatesPerEntityType { get; set; } = 10;
    public double MinimumCandidateScore { get; set; } = 0.20;
    public bool UseSourceConfidenceInScoring { get; set; } = true;
    public int MaxCandidateSearchPool { get; set; } = 50;

    public bool EnablePolicyEvaluation { get; set; } = true;
    public string PolicyProfileCode { get; set; } = "LIEN_INTAKE_POLICY_V1";
    public double MinimumRequiredFactConfidence { get; set; } = 0.70;
    public double MinimumPatientMatchScore { get; set; } = 0.85;
    public double MinimumProviderFacilityMatchScore { get; set; } = 0.80;
    public double MinimumCaseMatchScore { get; set; } = 0.75;
    public double MinimumPatientMatchMargin { get; set; } = 0.10;
    public double MinimumProviderFacilityMatchMargin { get; set; } = 0.10;
    public double MinimumCaseMatchMargin { get; set; } = 0.10;
    public bool RequirePatientMatch { get; set; } = true;
    public bool RequireProviderOrFacilityMatch { get; set; } = true;
    public bool RequireCaseMatch { get; set; }
    public bool RequireEvidenceForCriticalFacts { get; set; } = true;
    public bool ReviewOnAmbiguousFacts { get; set; } = true;
    public bool ReviewOnHardConflict { get; set; } = true;
    public bool BlockOnExactDuplicate { get; set; } = true;
    public bool ReviewOnPossibleDuplicate { get; set; } = true;
    public bool EnableAutoAcceptableDisposition { get; set; }

    public string? DestinationAdapterCode { get; set; }
}