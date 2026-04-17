namespace Flow.Application.Outbox;

/// <summary>
/// LS-FLOW-E10.2 — payload schemas serialised into
/// <c>OutboxMessage.PayloadJson</c>. Kept narrow on purpose: the worker
/// re-loads the workflow instance from the database when it processes
/// the row, so the payload only needs the minimum durable context the
/// handler needs to produce the audit/notification message and to make
/// idempotency decisions.
/// </summary>
public sealed record WorkflowLifecyclePayload(
    Guid WorkflowInstanceId,
    string ProductKey,
    string? FromStepKey,
    string? ToStepKey,
    string? FromStatus,
    string ToStatus,
    string? Reason,
    string? PerformedBy,
    DateTime OccurredAtUtc);

/// <summary>
/// LS-FLOW-E10.2 — payload for admin-action events (retry / force-complete
/// / cancel) emitted from <c>AdminWorkflowInstancesController</c>. Carries
/// the actor + reason so the audit handler can render a meaningful audit
/// description and the re-drive handler can short-circuit safely if the
/// state has moved on since the action committed.
/// </summary>
public sealed record AdminActionPayload(
    Guid WorkflowInstanceId,
    string ProductKey,
    string Action,
    string PreviousStatus,
    string NewStatus,
    string Reason,
    string PerformedBy,
    bool IsPlatformAdmin,
    DateTime OccurredAtUtc);
