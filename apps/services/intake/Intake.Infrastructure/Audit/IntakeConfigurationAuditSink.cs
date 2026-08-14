using System.Text.Json;
using Intake.Application.Configuration;
using LegalSynq.AuditClient;
using LegalSynq.AuditClient.DTOs;
using LegalSynq.AuditClient.Enums;
using Microsoft.Extensions.Logging;

namespace Intake.Infrastructure.Audit;

public sealed class IntakeConfigurationAuditSink(
    IAuditEventClient auditClient,
    ILogger<IntakeConfigurationAuditSink> logger) : IIntakeConfigurationAuditSink
{
    public async Task RecordAsync(
        ConfigurationAuditEntry entry,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await auditClient.IngestAsync(
                new IngestAuditEventRequest
                {
                    EventType = "intake.configuration.changed",
                    EventCategory = EventCategory.DataChange,
                    SourceSystem = "legalsynq-platform",
                    SourceService = "intake-service",
                    Visibility = VisibilityScope.Tenant,
                    Severity = SeverityLevel.Info,
                    OccurredAtUtc = DateTimeOffset.UtcNow,
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
                        Type = entry.ResourceType,
                        Id = entry.ResourceIdentifier,
                    },
                    Action = entry.Operation,
                    Description = $"Intake configuration {entry.Operation.ToLowerInvariant()}",
                    Outcome = "succeeded",
                    Before = entry.PreviousVersion?.ToString(),
                    After = entry.NewVersion.ToString(),
                    Metadata = JsonSerializer.Serialize(entry.Metadata),
                    CorrelationId = entry.CorrelationId,
                    Tags = ["intake", "configuration", "data-change"],
                },
                cancellationToken);

            if (!result.Accepted)
            {
                logger.LogWarning(
                    "Intake configuration audit event was not accepted. TenantId={TenantId} ResourceType={ResourceType} ResourceIdentifier={ResourceIdentifier} Operation={Operation} StatusCode={StatusCode} Reason={Reason} CorrelationId={CorrelationId}",
                    entry.TenantId,
                    entry.ResourceType,
                    entry.ResourceIdentifier,
                    entry.Operation,
                    result.StatusCode,
                    result.RejectionReason,
                    entry.CorrelationId);
            }
        }
        catch (Exception exception)
        {
            // Audit delivery must never fail a persisted configuration mutation.
            logger.LogWarning(
                exception,
                "Intake configuration audit delivery failed. TenantId={TenantId} ResourceType={ResourceType} ResourceIdentifier={ResourceIdentifier} Operation={Operation} CorrelationId={CorrelationId}",
                entry.TenantId,
                entry.ResourceType,
                entry.ResourceIdentifier,
                entry.Operation,
                entry.CorrelationId);
        }
    }
}