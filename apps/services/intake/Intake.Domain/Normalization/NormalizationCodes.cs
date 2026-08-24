namespace Intake.Domain.Normalization;

public static class NormalizationStatuses
{
    public const string NotNormalized = "NOT_NORMALIZED";
    public const string Normalized = "NORMALIZED";
    public const string Partial = "PARTIAL";
    public const string Invalid = "INVALID";
    public const string Ambiguous = "AMBIGUOUS";
    public const string Unsupported = "UNSUPPORTED";
}

public static class NormalizationRunStatuses
{
    public const string Processing = "PROCESSING";
    public const string Completed = "COMPLETED";
    public const string Partial = "PARTIAL";
    public const string Failed = "FAILED";
}

public static class ValidationStatuses
{
    public const string Valid = "VALID";
    public const string InvalidFormat = "INVALID_FORMAT";
    public const string Incomplete = "INCOMPLETE";
    public const string Ambiguous = "AMBIGUOUS";
    public const string Unverified = "UNVERIFIED";
}

public static class NormalizationFailureCodes
{
    public const string NormalizationDisabled = "NORMALIZATION_DISABLED";
    public const string ProfileUnavailable = "NORMALIZATION_PROFILE_UNAVAILABLE";
    public const string ExtractionRequired = "NORMALIZATION_EXTRACTION_REQUIRED";
    public const string ExtractionNotCurrent = "NORMALIZATION_EXTRACTION_NOT_CURRENT";
    public const string ConcurrencyConflict = "NORMALIZATION_CONCURRENCY_CONFLICT";
    public const string ExecutionFailed = "NORMALIZATION_FAILED";
}

public static class NormalizationWarningCodes
{
    public const string DateAmbiguous = "DATE_AMBIGUOUS";
    public const string DateRangeInvalid = "DATE_RANGE_INVALID";
    public const string DateCultureApplied = "DATE_CULTURE_APPLIED";
    public const string PhoneCountryAssumed = "PHONE_COUNTRY_ASSUMED";
    public const string AddressIncomplete = "ADDRESS_INCOMPLETE";
    public const string NameComponentsPartial = "NAME_COMPONENTS_PARTIAL";
    public const string CurrencyAssumed = "CURRENCY_ASSUMED";
    public const string IdentifierFormatUnrecognized = "IDENTIFIER_FORMAT_UNRECOGNIZED";
    public const string EmailInvalid = "EMAIL_INVALID";
    public const string OrganizationNormalizationPartial = "ORGANIZATION_NORMALIZATION_PARTIAL";
}