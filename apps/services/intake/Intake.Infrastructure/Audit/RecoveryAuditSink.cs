using System.Text.Json;
using Intake.Application.Operations;
using LegalSynq.AuditClient;
using LegalSynq.AuditClient.DTOs;
using LegalSynq.AuditClient.Enums;
using Microsoft.Extensions.Logging;

namespace Intake.Infrastructure.Audit;

public sealed class RecoveryAuditSink(
    IAuditEventClient auditClient,
    ILogger<RecoveryAuditSink> logger) : IRecoveryAuditSink
{
    public async Task RecordAsync(
        RecoveryAuditEntry entry,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await auditClient.IngestAsync(
                new IngestAuditEventRequest
                {
                    EventType = "intake.recovery",
                    EventCategory = EventCategory.Security,
                    SourceSystem = "legalsynq-platform",
                    SourceService = "intake-service",
                    Visibility = VisibilityScope.Tenant,
                    Scope = new AuditEventScopeDto
                    {
                        ScopeType = ScopeType.Tenant,
                        TenantId = entry.TenantId.ToString(),
                        UserId = entry.ActorUserId.ToString(),
                    },
                    Actor = new AuditEventActorDto
                    {
                        Type = ActorType.User,
                        Id = entry.ActorUserId.ToString(),
                    },
                    Entity = new AuditEventEntityDto
                    {
                        Type = "IntakeRecoveryWorkItem",
                        Id = entry.ObjectId.ToString(),
                    },
                    Action = entry.Action,
                    Description = "A tenant-scoped Intake recovery operation occurred.",
                    Outcome = entry.NewStatus,
                    Metadata = JsonSerializer.Serialize(new
                    {
                        entry.Stage,
                        entry.ObjectId,
                        entry.PreviousStatus,
                        entry.NewStatus,
                        entry.FailureCode,
                    }),
                    CorrelationId = entry.CorrelationId,
                    IdempotencyKey =
                        $"intake-recovery:{entry.TenantId}:{entry.Stage}:{entry.ObjectId}:{entry.Action}:{entry.NewStatus}",
                    Tags = ["intake", "recovery", "security"],
                },
                cancellationToken);
            if (!result.Accepted)
                logger.LogWarning(
                    "Recovery audit event was not accepted. TenantId={TenantId} Stage={Stage} ObjectId={ObjectId} Action={Action} StatusCode={StatusCode}",
                    entry.TenantId, entry.Stage, entry.ObjectId, entry.Action, result.StatusCode);
        }
        catch (Exception)
        {
            logger.LogWarning(
                "Recovery audit delivery failed. TenantId={TenantId} Stage={Stage} ObjectId={ObjectId}",
                entry.TenantId, entry.Stage, entry.ObjectId);
        }
    }
}