namespace Flow.Application.Interfaces;

/// <summary>
/// TASK-FLOW-01 — HTTP client interface for the canonical Task service.
/// Flow delegates task creation and lifecycle mutations to this client;
/// flow_workflow_tasks remains a read-only shadow (dual-write) in Phase 1.
///
/// <para>
/// Auth model: the implementation forwards the calling user's bearer token
/// via <c>FlowTaskServiceAuthDelegatingHandler</c>. All Task service endpoints
/// used here require <c>AuthenticatedUser</c> policy, so a valid user JWT must
/// be present in the ambient HTTP context when these methods are called.
/// </para>
///
/// <para>
/// Dual-write contract: callers invoke Task service first (making it the
/// write authority) and then mirror the change to <c>flow_workflow_tasks</c>
/// via the local EF context. If the Task service call fails, the local write
/// is NOT performed and the error propagates to the caller.
/// </para>
/// </summary>
public interface IFlowTaskServiceClient
{
    /// <summary>
    /// Creates a new task in the Task service for the given workflow step.
    /// Returns the canonical Task service ID assigned to the new task.
    /// Tenant context is derived from the forwarded bearer token.
    /// </summary>
    Task<Guid> CreateWorkflowTaskAsync(
        Guid      workflowInstanceId,
        string    stepKey,
        string    title,
        string    priority,
        DateTime? dueAt,
        string?   assignedUserId,
        CancellationToken ct = default);

    /// <summary>Transitions a task to <c>IN_PROGRESS</c> (Open → InProgress).</summary>
    Task StartTaskAsync(Guid taskId, CancellationToken ct = default);

    /// <summary>Transitions a task to <c>COMPLETED</c> (InProgress → Completed).</summary>
    Task CompleteTaskAsync(Guid taskId, CancellationToken ct = default);

    /// <summary>Transitions a task to <c>CANCELLED</c> (Open|InProgress → Cancelled).</summary>
    Task CancelTaskAsync(Guid taskId, CancellationToken ct = default);

    /// <summary>
    /// Assigns a task to a specific user (DirectUser mode).
    /// Pass <c>null</c> to clear the assignment (Unassigned).
    /// </summary>
    Task AssignUserAsync(Guid taskId, Guid? assignedUserId, CancellationToken ct = default);
}
