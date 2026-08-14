namespace Intake.Domain.Classification;

public sealed class ClassificationTaxonomyDefinition
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public int Version { get; set; }
    public string ClassesJson { get; set; } = "[]";
    public bool IsActive { get; set; }
    public bool IsSystemDefined { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}