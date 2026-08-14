using Intake.Application.Artifacts;
using LegalSynq.AuditClient;
using LegalSynq.AuditClient.DTOs;
using LegalSynq.AuditClient.Enums;
using Microsoft.Extensions.Logging;

namespace Intake.Infrastructure.Audit;

public sealed class EmailArtifactAuditSink(
    IAuditEventClient auditClient,
    ILogger<EmailArtifactAuditSink> logger) : IEmailArtifactAuditSink
{
    public async Task RecordAsync(
        EmailArtifactAuditEntry entry,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await auditClient.IngestAsync(
                new IngestAuditEventRequest
                {
                    EventType = "intake.email.artifacts.processed",
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
                        Type = "InboundEmail",
                        Id = entry.EmailId.ToString(),
                    },
                    Action = "PROCESS_ARTIFACTS",
                    Description = "Captured email artifacts were processed through the Documents Service.",
                    Outcome = entry.Status,
                    Metadata = System.Text.Json.JsonSerializer.Serialize(new
                    {
                        entry.ArtifactCount,
                        entry.CompletedCount,
                        entry.FailedCount,
                        entry.SkippedCount,
                    }),
                    CorrelationId = entry.CorrelationId,
                    IdempotencyKey = $"intake-artifacts:{entry.EmailId}:{entry.Status}:{entry.ArtifactCount}",
                    Tags = ["intake", "email-artifacts", "integration"],
                },
                cancellationToken);

            if (!result.Accepted)
            {
                logger.LogWarning(
                    "Email artifact audit event was not accepted. TenantId={TenantId} EmailId={EmailId} StatusCode={StatusCode} CorrelationId={CorrelationId}",
                    entry.TenantId,
                    entry.EmailId,
                    result.StatusCode,
                    entry.CorrelationId);
            }
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                exception,
                "Email artifact audit delivery failed. TenantId={TenantId} EmailId={EmailId} CorrelationId={CorrelationId}",
                entry.TenantId,
                entry.EmailId,
                entry.CorrelationId);
        }
    }
}