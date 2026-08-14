using Intake.Application.Extraction;
using LegalSynq.AuditClient;
using LegalSynq.AuditClient.DTOs;
using LegalSynq.AuditClient.Enums;
using Microsoft.Extensions.Logging;

namespace Intake.Infrastructure.Audit;

public sealed class ExtractionAuditSink(
    IAuditEventClient auditClient,
    ILogger<ExtractionAuditSink> logger) : IExtractionAuditSink
{
    public async Task RecordAsync(
        ExtractionAuditEntry entry,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await auditClient.IngestAsync(
                new IngestAuditEventRequest
                {
                    EventType = "intake.artifact.extraction",
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
                        Type = "ArtifactExtraction",
                        Id = entry.ExtractionId.ToString(),
                    },
                    Action = entry.Action,
                    Description = "An Intake artifact extraction attempt completed.",
                    Outcome = entry.Status,
                    Metadata = System.Text.Json.JsonSerializer.Serialize(new
                    {
                        entry.ArtifactId,
                        entry.ClassificationCode,
                        entry.FailureCode,
                    }),
                    CorrelationId = entry.CorrelationId,
                    IdempotencyKey = $"intake-extraction:{entry.ExtractionId}:{entry.Status}",
                    Tags = ["intake", "extraction", "ai"],
                },
                cancellationToken);
            if (!result.Accepted)
                logger.LogWarning(
                    "Extraction audit event was not accepted. Tenant={TenantId} Artifact={ArtifactId} StatusCode={StatusCode}",
                    entry.TenantId,
                    entry.ArtifactId,
                    result.StatusCode);
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                exception,
                "Extraction audit delivery failed. Tenant={TenantId} Artifact={ArtifactId}",
                entry.TenantId,
                entry.ArtifactId);
        }
    }
}