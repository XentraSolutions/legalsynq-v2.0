namespace Intake.Domain.Classification;

public sealed class ClassificationPromptDefinition
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public int Version { get; set; }
    public string Purpose { get; set; } = string.Empty;
    public string InstructionText { get; set; } = string.Empty;
    public string OutputSchemaJson { get; set; } = string.Empty;
    public int OutputSchemaVersion { get; set; }
    public bool IsActive { get; set; }
    public bool IsSystemDefined { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}