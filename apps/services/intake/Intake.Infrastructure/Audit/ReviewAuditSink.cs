using System.Text.Json;
using Intake.Application.Review;
using LegalSynq.AuditClient;
using LegalSynq.AuditClient.DTOs;
using LegalSynq.AuditClient.Enums;
using Microsoft.Extensions.Logging;

namespace Intake.Infrastructure.Audit;

public sealed class ReviewAuditSink(
    IAuditEventClient auditClient,
    ILogger<ReviewAuditSink> logger) : IReviewAuditSink
{
    public async Task RecordAsync(
        ReviewAuditEntry entry,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await auditClient.IngestAsync(
                new IngestAuditEventRequest
                {
                    EventType = $"intake.review.{entry.Action.ToLowerInvariant()}",
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
                        Type = "IntakeReview",
                        Id = entry.ReviewId.ToString(),
                    },
                    Action = entry.Action,
                    Description = "A tenant-scoped Intake human review action was recorded.",
                    Outcome = entry.Outcome ?? entry.Status,
                    Metadata = JsonSerializer.Serialize(new
                    {
                        entry.ArtifactId,
                        entry.Status,
                        entry.DecisionCode,
                        entry.Outcome,
                        entry.FactCode,
                        entry.EntityType,
                    }),
                    CorrelationId = entry.CorrelationId,
                    IdempotencyKey = $"intake-review:{entry.ReviewId}:{entry.Action}:{entry.DecisionCode}:{entry.Outcome}",
                    Tags = ["intake", "review"],
                },
                cancellationToken);
            if (!result.Accepted)
                logger.LogWarning(
                    "Review audit event was not accepted. Tenant={TenantId} Review={ReviewId} StatusCode={StatusCode}",
                    entry.TenantId,
                    entry.ReviewId,
                    result.StatusCode);
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                exception,
                "Review audit delivery failed. Tenant={TenantId} Review={ReviewId}",
                entry.TenantId,
                entry.ReviewId);
        }
    }
}