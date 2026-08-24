using System.Text.Json;
using Intake.Application.Policy;
using LegalSynq.AuditClient;
using LegalSynq.AuditClient.DTOs;
using LegalSynq.AuditClient.Enums;
using Microsoft.Extensions.Logging;

namespace Intake.Infrastructure.Audit;

public sealed class PolicyAuditSink(
    IAuditEventClient auditClient,
    ILogger<PolicyAuditSink> logger) : IPolicyAuditSink
{
    public async Task RecordAsync(
        PolicyAuditEntry entry,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await auditClient.IngestAsync(
                new IngestAuditEventRequest
                {
                    EventType = "intake.artifact.policy",
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
                        Type = "ArtifactPolicyEvaluation",
                        Id = entry.EvaluationId.ToString(),
                    },
                    Action = entry.Action,
                    Description = "A tenant-scoped Intake policy evaluation was processed.",
                    Outcome = entry.Status,
                    Metadata = JsonSerializer.Serialize(new
                    {
                        entry.ArtifactId,
                        entry.PolicyProfileCode,
                        entry.Disposition,
                        entry.ReviewPriority,
                        entry.FindingCount,
                        entry.FailureCode,
                    }),
                    CorrelationId = entry.CorrelationId,
                    IdempotencyKey = $"intake-policy:{entry.EvaluationId}:{entry.Action}",
                    Tags = ["intake", "policy"],
                },
                cancellationToken);
            if (!result.Accepted)
                logger.LogWarning(
                    "Policy audit event was not accepted. Tenant={TenantId} Artifact={ArtifactId} StatusCode={StatusCode}",
                    entry.TenantId,
                    entry.ArtifactId,
                    result.StatusCode);
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                exception,
                "Policy audit delivery failed. Tenant={TenantId} Artifact={ArtifactId}",
                entry.TenantId,
                entry.ArtifactId);
        }
    }
}