using System.Text.Json;
using Intake.Application.Snapshot;
using LegalSynq.AuditClient;
using LegalSynq.AuditClient.DTOs;
using LegalSynq.AuditClient.Enums;
using Microsoft.Extensions.Logging;

namespace Intake.Infrastructure.Audit;

public sealed class SnapshotAuditSink(
    IAuditEventClient auditClient,
    ILogger<SnapshotAuditSink> logger) : ISnapshotAuditSink
{
    public async Task RecordAsync(
        SnapshotAuditEntry entry,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await auditClient.IngestAsync(
                new IngestAuditEventRequest
                {
                    EventType = $"intake.snapshot.{entry.Action.ToLowerInvariant()}",
                    EventCategory = EventCategory.Business,
                    SourceSystem = "legalsynq-platform",
                    SourceService = "intake-service",
                    Visibility = VisibilityScope.Tenant,
                    Scope = new AuditEventScopeDto
                    {
                        ScopeType = ScopeType.Tenant,
                        TenantId = entry.TenantId.ToString(),
                        UserId = entry.ActorUserId?.ToString(),
                    },
                    Actor = new AuditEventActorDto
                    {
                        Type = ActorType.User,
                        Id = entry.ActorUserId?.ToString(),
                    },
                    Entity = new AuditEventEntityDto
                    {
                        Type = entry.AdapterCode is null
                            ? "ApprovedIntakeSnapshot"
                            : "IntakeAdapterExecution",
                        Id = entry.AdapterCode is null
                            ? entry.SnapshotId.ToString()
                            : entry.ExecutionId?.ToString(),
                    },
                    Action = entry.Action,
                    Description = "A tenant-scoped Intake approved snapshot or adapter event occurred.",
                    Outcome = entry.Status,
                    Metadata = JsonSerializer.Serialize(new
                    {
                        entry.SnapshotId,
                        entry.ArtifactId,
                        entry.ReviewId,
                        entry.Status,
                        entry.AdapterCode,
                        entry.ExecutionId,
                    }),
                    CorrelationId = entry.CorrelationId,
                    IdempotencyKey = $"intake-snapshot:{entry.TenantId}:{entry.SnapshotId}:{entry.Action}:{entry.ExecutionId}",
                    Tags = ["intake", "snapshot"],
                },
                cancellationToken);
            if (!result.Accepted)
                logger.LogWarning(
                    "Snapshot audit event was not accepted. Tenant={TenantId} Snapshot={SnapshotId} Action={Action} StatusCode={StatusCode}",
                    entry.TenantId,
                    entry.SnapshotId,
                    entry.Action,
                    result.StatusCode);
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                exception,
                "Snapshot audit delivery failed. Tenant={TenantId} Snapshot={SnapshotId} Action={Action}",
                entry.TenantId,
                entry.SnapshotId,
                entry.Action);
        }
    }
}