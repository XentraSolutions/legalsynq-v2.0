using System.Text.Json;
using Intake.Application.Matching;
using LegalSynq.AuditClient;
using LegalSynq.AuditClient.DTOs;
using LegalSynq.AuditClient.Enums;
using Microsoft.Extensions.Logging;

namespace Intake.Infrastructure.Audit;

public sealed class MatchingAuditSink(
    IAuditEventClient auditClient,
    ILogger<MatchingAuditSink> logger) : IMatchingAuditSink
{
    public async Task RecordAsync(
        MatchingAuditEntry entry,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await auditClient.IngestAsync(
                new IngestAuditEventRequest
                {
                    EventType = "intake.artifact.matching",
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
                        Type = "ArtifactMatchRun",
                        Id = entry.MatchRunId.ToString(),
                    },
                    Action = entry.Action,
                    Description = "A tenant-scoped Intake matching run was processed.",
                    Outcome = entry.Status,
                    Metadata = JsonSerializer.Serialize(new
                    {
                        entry.ArtifactId,
                        entry.ArtifactNormalizationId,
                        entry.MatchRunId,
                        entry.EntityTypesProcessed,
                        entry.CandidateCount,
                        entry.DuplicateCount,
                        entry.FailureCode,
                    }),
                    CorrelationId = entry.CorrelationId,
                    IdempotencyKey = $"intake-matching:{entry.MatchRunId}:{entry.Action}",
                    Tags = ["intake", "matching"],
                },
                cancellationToken);
            if (!result.Accepted)
                logger.LogWarning(
                    "Matching audit event was not accepted. Tenant={TenantId} Artifact={ArtifactId} StatusCode={StatusCode}",
                    entry.TenantId,
                    entry.ArtifactId,
                    result.StatusCode);
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                exception,
                "Matching audit delivery failed. Tenant={TenantId} Artifact={ArtifactId}",
                entry.TenantId,
                entry.ArtifactId);
        }
    }
}