namespace Intake.Domain.Sources;

public sealed class TenantIntakeSource
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid? OrgId { get; set; }
    public string SourceType { get; set; } = string.Empty;
    public string EmailAddress { get; set; } = string.Empty;
    public string NormalizedEmailAddress { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    public string Purpose { get; set; } = string.Empty;
    public string ProcessingProfileCode { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public bool IsDefault { get; set; }
    public string? DefaultTenantPurposeKey { get; set; }
    public string ConnectorConfigurationJson { get; set; } = "{}";
    public string? CredentialReference { get; set; }
    public string ValidationStatus { get; set; } = "NOT_VALIDATED";
    public DateTimeOffset? LastValidatedAt { get; set; }
    public string? LastValidationMessage { get; set; }
    public int ConfigurationVersion { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
}