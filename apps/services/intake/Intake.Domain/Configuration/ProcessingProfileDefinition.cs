namespace Intake.Domain.Configuration;

public sealed class ProcessingProfileDefinition
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int Version { get; set; }
    public bool IsActive { get; set; }
    public bool IsSystemDefined { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public ICollection<TenantProcessingProfile> TenantAssignments { get; set; } = [];
}