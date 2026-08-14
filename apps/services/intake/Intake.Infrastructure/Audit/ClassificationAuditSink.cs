using Intake.Application.Classification;
using LegalSynq.AuditClient;
using LegalSynq.AuditClient.DTOs;
using LegalSynq.AuditClient.Enums;
using Microsoft.Extensions.Logging;

namespace Intake.Infrastructure.Audit;

public sealed class ClassificationAuditSink(
    IAuditEventClient auditClient,
    ILogger<ClassificationAuditSink> logger) : IClassificationAuditSink
{
    public async Task RecordAsync(
        ClassificationAuditEntry entry,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await auditClient.IngestAsync(
                new IngestAuditEventRequest
                {
                    EventType = "intake.artifact.classification",
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
                        Type = "ArtifactClassification",
                        Id = entry.ClassificationId.ToString(),
                    },
                    Action = entry.Action,
                    Description = "An Intake artifact classification attempt completed.",
                    Outcome = entry.Status,
                    Metadata = System.Text.Json.JsonSerializer.Serialize(new
                    {
                        entry.ArtifactId,
                        entry.FailureCode,
                    }),
                    CorrelationId = entry.CorrelationId,
                    IdempotencyKey = $"intake-classification:{entry.ClassificationId}:{entry.Status}",
                    Tags = ["intake", "classification", "ai"],
                },
                cancellationToken);
            if (!result.Accepted)
                logger.LogWarning(
                    "Classification audit event was not accepted. Tenant={TenantId} Artifact={ArtifactId} StatusCode={StatusCode}",
                    entry.TenantId,
                    entry.ArtifactId,
                    result.StatusCode);
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                exception,
                "Classification audit delivery failed. Tenant={TenantId} Artifact={ArtifactId}",
                entry.TenantId,
                entry.ArtifactId);
        }
    }
}