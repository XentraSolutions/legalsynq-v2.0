using Flow.Domain.Common;

namespace Flow.Domain.Entities;

/// <summary>
/// LS-FLOW-E11.1 — first-class human-work item produced by workflow
/// execution. Distinct from the legacy <see cref="TaskItem"/> grain
/// (which lives at the *definition* layer and predates the dedicated
/// <see cref="WorkflowInstance"/> introduced in MERGE-P4).
///
/// <para>
/// <b>Layering:</b> WorkflowTask is the work-item layer; <see cref="WorkflowInstance"/>
/// remains the sole execution authority. This phase does NOT add any
/// engine wiring — no automatic creation on advance, no progression
/// binding, no APIs. Its only job here is to exist as a durable,
/// tenant-scoped, queryable surface that later phases (E11.2+) can
/// build creation/assignment/progression behaviour on top of.
/// </para>
///
/// <para>
/// <b>Linkage:</b> always points at a <see cref="WorkflowInstance"/>
/// (required) and the workflow <c>StepKey</c> it was raised against
/// (required, mirrors <see cref="WorkflowInstance.CurrentStepKey"/> /
/// <see cref="WorkflowStage.Key"/>). Multiple tasks per instance over
/// time are explicitly supported — no uniqueness constraint on
/// <c>(WorkflowInstanceId, StepKey)</c>.
/// </para>
///
/// <para>
/// <b>Tenant scoping:</b> <c>TenantId</c> (inherited from
/// <see cref="Common.BaseEntity"/>) is required and enforced by both
/// the <see cref="Infrastructure.Persistence.FlowDbContext"/> save hook
/// and a query filter on the entity, identical to every other Flow
/// grain.
/// </para>
///
/// <para>
/// <b>Domain invariants enforced (minimal):</b>
///   <list type="bullet">
///     <item><see cref="WorkflowInstanceId"/>, <see cref="StepKey"/>, <see cref="Status"/>, <c>TenantId</c> are required.</item>
///     <item>If <see cref="CompletedAt"/> is set, <see cref="Status"/> must be <see cref="WorkflowTaskStatus.Completed"/>.</item>
///     <item>If <see cref="CancelledAt"/> is set, <see cref="Status"/> must be <see cref="WorkflowTaskStatus.Cancelled"/>.</item>
///   </list>
/// Anything richer (open→in-progress only by assignee, no re-open of
/// terminal, etc.) is intentionally deferred to E11.2.
/// </para>
/// </summary>
public class WorkflowTask : AuditableEntity
{
    // ---------------- Linkage to execution layer -----------------

    /// <summary>Owning workflow instance (required).</summary>
    public Guid WorkflowInstanceId { get; set; }

    /// <summary>
    /// Workflow step the task was raised for. Stable string key that
    /// mirrors <see cref="WorkflowStage.Key"/> /
    /// <see cref="WorkflowInstance.CurrentStepKey"/>. Stored as a
    /// string (not an FK to <see cref="WorkflowStage"/>) so a task
    /// survives definition edits and so cross-version comparisons
    /// remain straightforward.
    /// </summary>
    public string StepKey { get; set; } = string.Empty;

    // ---------------- Descriptive payload -----------------

    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    // ---------------- Lifecycle state -----------------

    /// <summary>One of <see cref="WorkflowTaskStatus"/>. Defaults to <c>Open</c>.</summary>
    public string Status { get; set; } = WorkflowTaskStatus.Open;

    /// <summary>One of <see cref="WorkflowTaskPriority"/>. Defaults to <c>Normal</c>.</summary>
    public string Priority { get; set; } = WorkflowTaskPriority.Normal;

    // ---------------- Assignment placeholders (no logic yet) -----------------
    //
    // All three are nullable; nothing in this phase routes on them.
    // E11.2 will introduce the assignment resolver and any uniqueness
    // / mutual-exclusion rules between user/role/org.

    public string? AssignedUserId { get; set; }
    public string? AssignedRole   { get; set; }
    public string? AssignedOrgId  { get; set; }

    // ---------------- Lifecycle timestamps -----------------
    //
    // CreatedAt / UpdatedAt / CreatedBy / UpdatedBy come from
    // AuditableEntity. Started/Completed/Cancelled are domain-specific
    // and only set by the (future) lifecycle handlers — left null on
    // construction.

    public DateTime? StartedAt   { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime? CancelledAt { get; set; }

    // ---------------- Extensibility surfaces -----------------

    /// <summary>
    /// Free-form correlation key (external case number, ticket id,
    /// idempotency key, …). Indexed at the <c>(TenantId, …)</c> level
    /// in a future phase if query patterns demand it; for now it is a
    /// plain searchable column.
    /// </summary>
    public string? CorrelationKey { get; set; }

    /// <summary>
    /// Opaque JSON metadata bag. Kept as <c>longtext</c>/string to
    /// avoid imposing a schema before product use cases stabilise.
    /// Consumers should treat unknown keys as forward-compatible.
    /// </summary>
    public string? MetadataJson { get; set; }

    // ---------------- Navigation -----------------

    /// <summary>The owning workflow instance. Restrict-on-delete at the FK.</summary>
    public WorkflowInstance? WorkflowInstance { get; set; }

    // ---------------- Domain invariants -----------------

    /// <summary>
    /// Validates the minimal invariants documented on this type. Called
    /// from <see cref="Infrastructure.Persistence.FlowDbContext.SaveChangesAsync"/>
    /// for added / modified rows; throws <see cref="InvalidOperationException"/>
    /// on violation so the writer fails loudly rather than persisting an
    /// internally inconsistent task.
    /// </summary>
    public void EnsureValid()
    {
        if (WorkflowInstanceId == Guid.Empty)
            throw new InvalidOperationException("WorkflowTask.WorkflowInstanceId is required.");
        if (string.IsNullOrWhiteSpace(StepKey))
            throw new InvalidOperationException("WorkflowTask.StepKey is required.");
        if (string.IsNullOrWhiteSpace(Status))
            throw new InvalidOperationException("WorkflowTask.Status is required.");

        if (CompletedAt is not null && !string.Equals(Status, WorkflowTaskStatus.Completed, StringComparison.Ordinal))
            throw new InvalidOperationException(
                $"WorkflowTask.CompletedAt is set but Status='{Status}' (expected '{WorkflowTaskStatus.Completed}').");

        if (CancelledAt is not null && !string.Equals(Status, WorkflowTaskStatus.Cancelled, StringComparison.Ordinal))
            throw new InvalidOperationException(
                $"WorkflowTask.CancelledAt is set but Status='{Status}' (expected '{WorkflowTaskStatus.Cancelled}').");
    }
}
