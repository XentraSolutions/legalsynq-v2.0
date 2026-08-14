namespace Intake.Domain.Extraction;

public sealed class ExtractionProfileDefinition
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int Version { get; set; }
    public string SchemaCode { get; set; } = string.Empty;
    public int SchemaVersion { get; set; }
    public string PromptCode { get; set; } = string.Empty;
    public int PromptVersion { get; set; }
    public int OutputSchemaVersion { get; set; }
    public bool IsActive { get; set; }
    public bool IsSystemDefined { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class ExtractionSchemaDefinition
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string ClassificationCode { get; set; } = string.Empty;
    public int Version { get; set; }
    public string FactCatalogJson { get; set; } = string.Empty;
    public string OutputSchemaJson { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public bool IsSystemDefined { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class ExtractionPromptDefinition
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public int Version { get; set; }
    public string ClassificationCode { get; set; } = string.Empty;
    public string Purpose { get; set; } = string.Empty;
    public string InstructionText { get; set; } = string.Empty;
    public int OutputSchemaVersion { get; set; }
    public bool IsActive { get; set; }
    public bool IsSystemDefined { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}