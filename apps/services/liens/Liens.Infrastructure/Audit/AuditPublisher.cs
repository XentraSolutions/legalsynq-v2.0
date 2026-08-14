using Liens.Application.Interfaces;
using LegalSynq.AuditClient;
using LegalSynq.AuditClient.DTOs;
using LegalSynq.AuditClient.Enums;
using Microsoft.Extensions.Logging;

namespace Liens.Infrastructure.Audit;

public sealed class AuditPublisher : IAuditPublisher
{
    private readonly IAuditEventClient _client;
    private readonly ILogger<AuditPublisher> _logger;
    private List<PendingAuditPublication>? _bufferedPublications;

    public AuditPublisher(IAuditEventClient client, ILogger<AuditPublisher> logger)
    {
        _client = client;
        _logger = logger;
    }

    public IAuditPublicationBuffer BeginBuffer()
    {
        if (_bufferedPublications is not null)
            throw new InvalidOperationException("An audit publication buffer is already active.");

        _bufferedPublications = [];
        return new AuditPublicationBuffer(this);
    }

    public void Publish(
        string eventType,
        string action,
        string description,
        Guid tenantId,
        Guid? actorUserId = null,
        string? entityType = null,
        string? entityId = null,
        string? before = null,
        string? after = null,
        string? metadata = null)
    {
        if (_bufferedPublications is not null)
        {
            _bufferedPublications.Add(new PendingAuditPublication(
                eventType,
                action,
                description,
                tenantId,
                actorUserId,
                entityType,
                entityId,
                before,
                after,
                metadata));
            return;
        }

        PublishImmediately(
            eventType,
            action,
            description,
            tenantId,
            actorUserId,
            entityType,
            entityId,
            before,
            after,
            metadata);
    }

    private void PublishImmediately(
        string eventType,
        string action,
        string description,
        Guid tenantId,
        Guid? actorUserId,
        string? entityType,
        string? entityId,
        string? before,
        string? after,
        string? metadata)
    {
        var now = DateTimeOffset.UtcNow;
        var request = new IngestAuditEventRequest
        {
            EventType = eventType,
            EventCategory = EventCategory.Business,
            SourceSystem = "liens-service",
            SourceService = "liens-api",
            Visibility = VisibilityScope.Tenant,
            Severity = SeverityLevel.Info,
            OccurredAtUtc = now,
            Scope = new AuditEventScopeDto
            {
                ScopeType = ScopeType.Tenant,
                TenantId = tenantId.ToString(),
            },
            Actor = new AuditEventActorDto
            {
                Type = actorUserId.HasValue ? ActorType.User : ActorType.System,
                Id = actorUserId?.ToString(),
            },
            Entity = entityType != null
                ? new AuditEventEntityDto { Type = entityType, Id = entityId }
                : null,
            Action = action,
            Description = description,
            Before = before,
            After = after,
            Metadata = metadata,
            IdempotencyKey = IdempotencyKey.For(
                "liens-service", eventType, entityId ?? tenantId.ToString(), now.UtcTicks.ToString()),
            Tags = ["liens"],
        };

        _ = _client.IngestAsync(request).ContinueWith(t =>
        {
            if (t.IsFaulted)
                _logger.LogWarning(t.Exception, "Audit publish failed for {EventType}", eventType);
        }, TaskContinuationOptions.OnlyOnFaulted);
    }

    private void CommitBuffer()
    {
        var publications = _bufferedPublications
            ?? throw new InvalidOperationException("No audit publication buffer is active.");
        _bufferedPublications = null;

        foreach (var publication in publications)
        {
            PublishImmediately(
                publication.EventType,
                publication.Action,
                publication.Description,
                publication.TenantId,
                publication.ActorUserId,
                publication.EntityType,
                publication.EntityId,
                publication.Before,
                publication.After,
                publication.Metadata);
        }
    }

    private void DiscardBuffer() => _bufferedPublications = null;

    private sealed class AuditPublicationBuffer : IAuditPublicationBuffer
    {
        private AuditPublisher? _owner;

        public AuditPublicationBuffer(AuditPublisher owner)
        {
            _owner = owner;
        }

        public void Commit()
        {
            var owner = Interlocked.Exchange(ref _owner, null)
                ?? throw new InvalidOperationException("The audit publication buffer is already complete.");
            owner.CommitBuffer();
        }

        public void Dispose()
        {
            Interlocked.Exchange(ref _owner, null)?.DiscardBuffer();
        }
    }

    private sealed record PendingAuditPublication(
        string EventType,
        string Action,
        string Description,
        Guid TenantId,
        Guid? ActorUserId,
        string? EntityType,
        string? EntityId,
        string? Before,
        string? After,
        string? Metadata);
}
