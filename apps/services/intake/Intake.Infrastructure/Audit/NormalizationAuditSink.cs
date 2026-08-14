using System.Text.Json;
using Intake.Application.Normalization;
using LegalSynq.AuditClient;
using LegalSynq.AuditClient.DTOs;
using LegalSynq.AuditClient.Enums;
using Microsoft.Extensions.Logging;

namespace Intake.Infrastructure.Audit;

public sealed class NormalizationAuditSink(
    IAuditEventClient auditClient,
    ILogger<NormalizationAuditSink> logger) : INormalizationAuditSink
{
    public async Task RecordAsync(
        NormalizationAuditEntry entry,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await auditClient.IngestAsync(
                new IngestAuditEventRequest
                {
                    EventType = "intake.artifact.normalization",
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
                        Type = "ArtifactNormalization",
                        Id = entry.NormalizationId.ToString(),
                    },
                    Action = entry.Action,
                    Description = "An Intake artifact normalization attempt completed.",
                    Outcome = entry.Status,
                    Metadata = JsonSerializer.Serialize(new
                    {
                        entry.ArtifactId,
                        entry.ArtifactExtractionId,
                        entry.ProfileCode,
                        entry.FactCount,
                        entry.NormalizedCount,
                        entry.InvalidCount,
                        entry.AmbiguousCount,
                    }),
                    CorrelationId = entry.CorrelationId,
                    IdempotencyKey = $"intake-normalization:{entry.NormalizationId}:{entry.Status}",
                    Tags = ["intake", "normalization"],
                },
                cancellationToken);
            if (!result.Accepted)
                logger.LogWarning(
                    "Normalization audit event was not accepted. Tenant={TenantId} Artifact={ArtifactId} StatusCode={StatusCode}",
                    entry.TenantId,
                    entry.ArtifactId,
                    result.StatusCode);
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                exception,
                "Normalization audit delivery failed. Tenant={TenantId} Artifact={ArtifactId}",
                entry.TenantId,
                entry.ArtifactId);
        }
    }
}