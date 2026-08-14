namespace Intake.Domain.Classification;

public sealed class ClassificationProfileDefinition
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int Version { get; set; }
    public string TaxonomyCode { get; set; } = string.Empty;
    public int TaxonomyVersion { get; set; }
    public string PromptCode { get; set; } = string.Empty;
    public int PromptVersion { get; set; }
    public int OutputSchemaVersion { get; set; }
    public bool IsActive { get; set; }
    public bool IsSystemDefined { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}