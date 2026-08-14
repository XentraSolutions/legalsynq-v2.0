using System.Text.Json;

namespace Intake.Contracts.Configuration;

public sealed record ProcessingProfileDefinitionResponse(
    string Code,
    string DisplayName,
    string? Description,
    int Version,
    bool IsActive,
    bool IsSystemDefined);

public sealed record TenantIntakeConfigurationResponse(
    Guid TenantId,
    Guid? OrgId,
    bool IsEnabled,
    string? DefaultProcessingProfileCode,
    bool RequireHumanReviewByDefault,
    bool AutoProcessingEnabled,
    int ConfigurationVersion,
    DateTimeOffset CreatedAt,
    Guid? CreatedBy,
    DateTimeOffset UpdatedAt,
    Guid? UpdatedBy);

public sealed record TenantProcessingProfileResponse(
    Guid TenantId,
    string ProcessingProfileCode,
    string DisplayName,
    int ProcessingProfileVersion,
    bool IsEnabled,
    bool IsDefault,
    JsonElement Configuration,
    int ConfigurationVersion,
    DateTimeOffset CreatedAt,
    Guid? CreatedBy,
    DateTimeOffset UpdatedAt,
    Guid? UpdatedBy);

public sealed record ResolvedProcessingConfiguration(
    Guid TenantId,
    string ProcessingProfileCode,
    int ProcessingProfileVersion,
    int TenantConfigurationVersion,
    int TenantProfileConfigurationVersion,
    LienIntakeV1Configuration EffectiveConfiguration,
    DateTimeOffset ResolvedAt);

public sealed class UpsertTenantIntakeConfigurationRequest
{
    public bool? IsEnabled { get; set; }
    public string? DefaultProcessingProfileCode { get; set; }
    public bool? RequireHumanReviewByDefault { get; set; }
    public bool? AutoProcessingEnabled { get; set; }
    public int? ConfigurationVersion { get; set; }
}

public sealed class AssignTenantProcessingProfileRequest
{
    public string ProcessingProfileCode { get; set; } = string.Empty;
    public bool IsDefault { get; set; }
    public JsonElement? Configuration { get; set; }
}

public sealed class UpdateTenantProcessingProfileRequest
{
    public JsonElement? Configuration { get; set; }
    public bool? IsDefault { get; set; }
    public int? ConfigurationVersion { get; set; }
}

public sealed class UpdateTenantProcessingProfileStatusRequest
{
    public bool IsEnabled { get; set; }
    public int? ConfigurationVersion { get; set; }
}