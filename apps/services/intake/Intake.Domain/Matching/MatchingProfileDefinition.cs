namespace Intake.Domain.Matching;

public sealed class MatchingProfileDefinition
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int Version { get; set; }
    public string ScoringVersion { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public bool IsSystemDefined { get; set; }
    public string DefinitionJson { get; set; } = "{}";
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}