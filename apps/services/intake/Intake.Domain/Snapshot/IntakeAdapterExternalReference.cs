namespace Intake.Domain.Snapshot;

public sealed class IntakeAdapterExternalReference
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid AdapterExecutionId { get; set; }
    public string ReferenceType { get; set; } = string.Empty;
    public string ReferenceId { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
}