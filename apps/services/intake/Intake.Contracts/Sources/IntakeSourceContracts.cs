using System.Text.Json;

namespace Intake.Contracts.Sources;

public sealed record IntakeSourceResponse(
    Guid SourceId,
    Guid TenantId,
    Guid? OrgId,
    string SourceType,
    string EmailAddress,
    string NormalizedEmailAddress,
    string Provider,
    string Purpose,
    string ProcessingProfileCode,
    bool IsActive,
    bool IsDefault,
    string? CredentialReference,
    JsonElement ConnectorConfiguration,
    string ValidationStatus,
    DateTimeOffset? LastValidatedAt,
    string? LastValidationMessage,
    int ConfigurationVersion,
    DateTimeOffset CreatedAt,
    Guid? CreatedBy,
    DateTimeOffset UpdatedAt,
    Guid? UpdatedBy);

public sealed record ResolvedIntakeSource(
    Guid SourceId,
    Guid TenantId,
    Guid? OrgId,
    string SourceType,
    string EmailAddress,
    string NormalizedEmailAddress,
    string Purpose,
    string Provider,
    string ProcessingProfileCode,
    int SourceConfigurationVersion,
    DateTimeOffset ResolvedAt);

public sealed record IntakeSourceTypeResponse(string Code, string DisplayName);

public sealed record IntakeSourcePurposeResponse(string Code, string DisplayName);

public sealed record EmailConnectorCapabilitiesResponse(
    bool SupportsPolling,
    bool SupportsWebhook,
    bool SupportsOAuth,
    bool SupportsAttachmentRetrieval,
    bool SupportsMessageIdLookup,
    bool SupportsMailboxFolders);

public sealed record EmailConnectorDefinitionResponse(
    string Code,
    string DisplayName,
    bool ConfigurationOnly,
    EmailConnectorCapabilitiesResponse Capabilities);

public sealed record ConnectorTestResponse(
    Guid SourceId,
    string Provider,
    string Status,
    string Message,
    DateTimeOffset TestedAt);

public sealed record SourceValidationResponse(
    IntakeSourceResponse Source,
    string ValidationStatus,
    string Message);

public sealed class CreateIntakeSourceRequest
{
    public string SourceType { get; set; } = string.Empty;
    public string EmailAddress { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    public string Purpose { get; set; } = string.Empty;
    public string ProcessingProfileCode { get; set; } = string.Empty;
    public bool IsDefault { get; set; }
    public JsonElement? ConnectorConfiguration { get; set; }
    public string? CredentialReference { get; set; }
}

public sealed class UpdateIntakeSourceRequest
{
    public string SourceType { get; set; } = string.Empty;
    public string EmailAddress { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    public string Purpose { get; set; } = string.Empty;
    public string ProcessingProfileCode { get; set; } = string.Empty;
    public bool IsDefault { get; set; }
    public JsonElement? ConnectorConfiguration { get; set; }
    public string? CredentialReference { get; set; }
    public int? ConfigurationVersion { get; set; }
}

public sealed class UpdateIntakeSourceStatusRequest
{
    public bool IsActive { get; set; }
    public int? ConfigurationVersion { get; set; }
}

public sealed class ValidateIntakeSourceRequest
{
    public int? ConfigurationVersion { get; set; }
}