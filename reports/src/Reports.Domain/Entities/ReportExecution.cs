namespace Reports.Domain.Entities;

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
