namespace Intake.Domain.Configuration;

public sealed class TenantProcessingProfile
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid ProcessingProfileDefinitionId { get; set; }
    public ProcessingProfileDefinition? ProcessingProfileDefinition { get; set; }
    public bool IsEnabled { get; set; }
    public bool IsDefault { get; set; }
    public string? DefaultTenantKey { get; set; }
    public string ConfigurationJson { get; set; } = "{}";
    public int ConfigurationVersion { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
}