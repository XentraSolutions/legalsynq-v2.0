namespace Reports.Domain.Entities;

/// <summary>
/// Bootstrap placeholder — tracks a single execution of a report.
/// <para>
/// This entity is scaffolding introduced in Epic 00 (LS-REPORTS-00-001) to
/// establish the domain layer shape. It is NOT a finalized domain model.
/// Do not expand with business logic or additional properties until the
/// persistence story (LS-REPORTS-00-002+) formally designs the schema.
/// </para>
/// </summary>
public sealed class ReportExecution
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string TenantId { get; init; } = string.Empty;
    public string UserId { get; init; } = string.Empty;
    public string ReportDefinitionCode { get; init; } = string.Empty;
    public string Status { get; init; } = "Pending";
    public string? OutputDocumentId { get; init; }
    public string? FailureReason { get; init; }
    public DateTimeOffset CreatedAtUtc { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? CompletedAtUtc { get; init; }
}
