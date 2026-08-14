namespace Intake.Domain.Classification;

public static class SynqAiAccessModes
{
    public const string LegalSynqManaged = "LEGALSYNQ_MANAGED";
    public const string BringYourOwn = "BYOAI";
}

public static class SynqAiProviderCodes
{
    public const string OpenAi = "OPENAI";
}

public static class ClassificationStatuses
{
    public const string Pending = "PENDING";
    public const string Processing = "PROCESSING";
    public const string Completed = "COMPLETED";
    public const string Failed = "FAILED";
    public const string Skipped = "SKIPPED";
}

public static class ClassificationDecisionStatuses
{
    public const string Accepted = "ACCEPTED";
    public const string LowConfidence = "LOW_CONFIDENCE";
    public const string Unclassified = "UNCLASSIFIED";
}

public static class ClassificationFailureCodes
{
    public const string PolicyMissing = "AI_POLICY_MISSING";
    public const string PolicyDisabled = "AI_POLICY_DISABLED";
    public const string ProviderUnavailable = "AI_PROVIDER_UNAVAILABLE";
    public const string CredentialUnavailable = "AI_CREDENTIAL_UNAVAILABLE";
    public const string ProfileMissing = "CLASSIFICATION_PROFILE_MISSING";
    public const string TaxonomyInvalid = "CLASSIFICATION_TAXONOMY_INVALID";
    public const string PromptInvalid = "CLASSIFICATION_PROMPT_INVALID";
    public const string ArtifactNotEligible = "ARTIFACT_NOT_ELIGIBLE";
    public const string ArtifactHashMissing = "ARTIFACT_HASH_MISSING";
    public const string ArtifactHashChanged = "ARTIFACT_HASH_CHANGED";
    public const string InputTooLarge = "CLASSIFICATION_INPUT_TOO_LARGE";
    public const string UnsupportedContent = "CLASSIFICATION_CONTENT_UNSUPPORTED";
    public const string ProviderTimeout = "AI_PROVIDER_TIMEOUT";
    public const string ProviderRejected = "AI_PROVIDER_REJECTED";
    public const string ProviderResponseInvalid = "AI_RESPONSE_INVALID";
    public const string SchemaValidationFailed = "AI_SCHEMA_VALIDATION_FAILED";
    public const string ConcurrencyConflict = "CLASSIFICATION_CONCURRENCY_CONFLICT";
    public const string RetryLimitExceeded = "AI_RETRY_LIMIT_EXCEEDED";
}