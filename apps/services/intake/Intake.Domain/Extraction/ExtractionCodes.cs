namespace Intake.Domain.Extraction;

public static class ExtractionStatuses
{
    public const string Pending = "PENDING";
    public const string Processing = "PROCESSING";
    public const string Completed = "COMPLETED";
    public const string Failed = "FAILED";
    public const string Skipped = "SKIPPED";
}

public static class ExtractionFailureCodes
{
    public const string ExtractionDisabled = "EXTRACTION_DISABLED";
    public const string PolicyMissing = "AI_POLICY_MISSING";
    public const string PolicyDisabled = "AI_POLICY_DISABLED";
    public const string ProviderUnavailable = "AI_PROVIDER_UNAVAILABLE";
    public const string CredentialUnavailable = "AI_CREDENTIAL_UNAVAILABLE";
    public const string ProfileMissing = "EXTRACTION_PROFILE_MISSING";
    public const string SchemaMissing = "EXTRACTION_SCHEMA_MISSING";
    public const string PromptMissing = "EXTRACTION_PROMPT_MISSING";
    public const string SchemaInvalid = "EXTRACTION_SCHEMA_INVALID";
    public const string PromptInvalid = "EXTRACTION_PROMPT_INVALID";
    public const string ClassificationRequired = "CLASSIFICATION_REQUIRED";
    public const string ClassificationUnsupported = "CLASSIFICATION_UNSUPPORTED";
    public const string ArtifactNotEligible = "ARTIFACT_NOT_ELIGIBLE";
    public const string ArtifactHashMissing = "ARTIFACT_HASH_MISSING";
    public const string ArtifactHashChanged = "ARTIFACT_HASH_CHANGED";
    public const string InputTooLarge = "EXTRACTION_INPUT_TOO_LARGE";
    public const string UnsupportedContent = "EXTRACTION_CONTENT_UNSUPPORTED";
    public const string ProviderTimeout = "AI_PROVIDER_TIMEOUT";
    public const string ProviderRejected = "AI_PROVIDER_REJECTED";
    public const string ProviderResponseInvalid = "AI_RESPONSE_INVALID";
    public const string SchemaValidationFailed = "AI_SCHEMA_VALIDATION_FAILED";
    public const string FactValidationFailed = "EXTRACTED_FACT_INVALID";
    public const string ConcurrencyConflict = "EXTRACTION_CONCURRENCY_CONFLICT";
    public const string RetryLimitExceeded = "AI_RETRY_LIMIT_EXCEEDED";
}

public static class ExtractionFactDataTypes
{
    public const string Text = "TEXT";
    public const string Name = "NAME";
    public const string Identifier = "IDENTIFIER";
    public const string Money = "MONEY";
    public const string Date = "DATE";
    public const string Address = "ADDRESS";
    public const string Boolean = "BOOLEAN";
}