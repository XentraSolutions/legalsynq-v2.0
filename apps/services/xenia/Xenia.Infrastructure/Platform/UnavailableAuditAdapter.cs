using Microsoft.Extensions.Logging;
using Xenia.Application.Adapters.Interfaces;

namespace Xenia.Infrastructure.Platform;

/// <summary>
/// Fallback implementation of <see cref="IAuditAdapter"/> for environments
/// where the external Audit service is not configured.
///
/// IMPORTANT: Audit events are NEVER silently discarded. This implementation
/// logs every event at Warning level with a [AUDIT-FALLBACK] prefix so that
/// events are captured in the service's structured log and can be recovered
/// or replayed when the Audit service becomes available.
/// </summary>
internal sealed class UnavailableAuditAdapter : IAuditAdapter
{
    private readonly ILogger<UnavailableAuditAdapter> _logger;

    public UnavailableAuditAdapter(ILogger<UnavailableAuditAdapter> logger)
        => _logger = logger;

    public bool IsConfigured => false;

    public Task RecordEventAsync(XeniaAuditEvent auditEvent, CancellationToken ct = default)
    {
        _logger.LogWarning(
            "[AUDIT-FALLBACK] Audit service unavailable. Event recorded to log only. " +
            "Action={Action} Resource={ResourceType}/{ResourceId} Result={Result} " +
            "TenantId={TenantId} ActorId={ActorId} CorrelationId={CorrelationId} " +
            "OccurredAt={OccurredAt} Detail={Detail}",
            auditEvent.Action,
            auditEvent.ResourceType,
            auditEvent.ResourceId ?? "n/a",
            auditEvent.Result,
            auditEvent.TenantId,
            auditEvent.ActorId,
            auditEvent.CorrelationId ?? "n/a",
            auditEvent.OccurredAt,
            auditEvent.Detail ?? string.Empty);

        return Task.CompletedTask;
    }
}
