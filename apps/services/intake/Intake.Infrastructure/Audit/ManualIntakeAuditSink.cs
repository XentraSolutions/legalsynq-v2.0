using Intake.Application.Manual;
using LegalSynq.AuditClient;
using LegalSynq.AuditClient.DTOs;
using LegalSynq.AuditClient.Enums;
using Microsoft.Extensions.Logging;

namespace Intake.Infrastructure.Audit;

public sealed class ManualIntakeAuditSink(
    IAuditEventClient auditClient,
    ILogger<ManualIntakeAuditSink> logger) : IManualIntakeAuditSink
{
    public async Task RecordAsync(
        ManualIntakeAuditEntry entry,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await auditClient.IngestAsync(
                new IngestAuditEventRequest
                {
                    EventType = "intake.manual.submission",
                    EventCategory = EventCategory.Integration,
                    SourceSystem = "legalsynq-platform",
                    SourceService = "intake-service",
                    Visibility = VisibilityScope.Tenant,
                    Scope = new AuditEventScopeDto
                    {
                        ScopeType = ScopeType.Tenant,
                        TenantId = entry.TenantId.ToString(),
                        UserId = entry.ActorId?.ToString(),
                    },
                    Actor = new AuditEventActorDto
                    {
                        Type = ActorType.User,
                        Id = entry.ActorId?.ToString(),
                    },
                    Entity = new AuditEventEntityDto
                    {
                        Type = "ManualIntakeSubmission",
                        Id = entry.SubmissionId.ToString(),
                    },
                    Action = entry.Action,
                    Description = "A manual Intake submission was processed.",
                    Outcome = entry.Status,
                    Metadata = System.Text.Json.JsonSerializer.Serialize(new
                    {
                        entry.ArtifactCount,
                        entry.CompletedCount,
                        entry.FailedCount,
                        entry.SkippedCount,
                    }),
                    CorrelationId = entry.CorrelationId,
                    IdempotencyKey = $"intake-manual:{entry.SubmissionId}:{entry.Action}:{entry.Status}",
                    Tags = ["intake", "manual", "artifacts"],
                },
                cancellationToken);

            if (!result.Accepted)
                logger.LogWarning(
                    "Manual Intake audit event was not accepted. TenantId={TenantId} SubmissionId={SubmissionId} StatusCode={StatusCode}",
                    entry.TenantId,
                    entry.SubmissionId,
                    result.StatusCode);
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                exception,
                "Manual Intake audit delivery failed. TenantId={TenantId} SubmissionId={SubmissionId}",
                entry.TenantId,
                entry.SubmissionId);
        }
    }
}