namespace Intake.Domain.Normalization;

public sealed class NormalizationProfileDefinition
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int Version { get; set; }
    public bool IsActive { get; set; }
    public bool IsSystemDefined { get; set; }
    public string SupportedFactCodesJson { get; set; } = "[]";
    public string NormalizerVersion { get; set; } = "1";
    public string UnicodeForm { get; set; } = "NFKC";
    public string ComparisonKeyStrategy { get; set; } = "UPPER_ASCII_ALNUM";
    public string DefaultDateCulture { get; set; } = "en-US";
    public string DefaultCountryCode { get; set; } = "US";
    public string DefaultCurrencyCode { get; set; } = "USD";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}