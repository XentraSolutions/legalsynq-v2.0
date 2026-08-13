namespace Intake.Domain.Configuration;

public sealed class TenantIntakeConfiguration
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid? OrgId { get; set; }
    public bool IsEnabled { get; set; }
    public string? DefaultProcessingProfileCode { get; set; }
    public bool RequireHumanReviewByDefault { get; set; }
    public bool AutoProcessingEnabled { get; set; }
    public int ConfigurationVersion { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
}