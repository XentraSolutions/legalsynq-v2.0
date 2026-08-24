namespace Intake.Domain.Emails;

public sealed class InboundEmailCaptureFailure
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid TenantIntakeSourceId { get; set; }
    public string? Provider { get; set; }
    public string FailureCode { get; set; } = string.Empty;
    public DateTimeOffset OccurredAt { get; set; }
    public string? CorrelationId { get; set; }
}