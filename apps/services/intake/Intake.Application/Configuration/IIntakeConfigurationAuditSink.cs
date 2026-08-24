namespace Intake.Application.Configuration;

public sealed record ConfigurationAuditEntry(
    Guid TenantId,
    string ResourceType,
    string ResourceIdentifier,
    string Operation,
    int? PreviousVersion,
    int NewVersion,
    Guid? ActorId,
    string? CorrelationId,
    object? Metadata);

public interface IIntakeConfigurationAuditSink
{
    Task RecordAsync(ConfigurationAuditEntry entry, CancellationToken cancellationToken);
}