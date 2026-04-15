namespace Reports.Domain.Entities;

public sealed class ReportTemplateVersion
{
    public Guid Id { get; set; }
    public Guid ReportDefinitionId { get; set; }
    public int VersionNumber { get; set; } = 1;
    public string? TemplateBody { get; set; }
    public string OutputFormat { get; set; } = "PDF";
    public string? ChangeNotes { get; set; }
    public bool IsActive { get; set; } = true;
    public string CreatedByUserId { get; set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public ReportDefinition? ReportDefinition { get; set; }
}
