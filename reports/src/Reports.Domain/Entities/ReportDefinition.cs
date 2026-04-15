namespace Reports.Domain.Entities;

/// <summary>
/// Bootstrap placeholder — defines the metadata for a type of report.
/// <para>
/// This entity is scaffolding introduced in Epic 00 (LS-REPORTS-00-001) to
/// establish the domain layer shape. It is NOT a finalized domain model.
/// Do not expand with business logic or additional properties until the
/// persistence story (LS-REPORTS-00-002+) formally designs the schema.
/// </para>
/// </summary>
public sealed class ReportDefinition
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string ProductCode { get; init; } = string.Empty;
    public bool IsActive { get; init; } = true;
    public DateTimeOffset CreatedAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
