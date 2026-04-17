using Flow.Domain.Common;

namespace Flow.Domain.Entities;

/// <summary>
/// LS-FLOW-MERGE-P4 — dedicated workflow-instance grain. Replaces the
/// Phase-3 surrogate that overloaded <see cref="TaskItem"/> as the
/// instance pointer in <see cref="ProductWorkflowMapping"/>.
///
/// A WorkflowInstance is the runtime activation of a <see cref="FlowDefinition"/>
/// for a specific tenant + product. It owns lifecycle state and (optionally)
/// references the initial driving task; the underlying TaskItem(s) still
/// drive day-to-day execution, so this is not a workflow-engine rewrite —
/// it's the canonical correlation grain that products consume.
/// </summary>
public class WorkflowInstance : AuditableEntity
{
    /// <summary>Definition this instance was started from.</summary>
    public Guid WorkflowDefinitionId { get; set; }

    /// <summary>Product this instance belongs to (mirrors definition's ProductKey).</summary>
    public string ProductKey { get; set; } = ProductKeys.FlowGeneric;

    /// <summary>Free-form correlation key (external case number, etc.).</summary>
    public string? CorrelationKey { get; set; }

    /// <summary>
    /// Optional initial driving task. Today every instance is bootstrapped
    /// from one TaskItem; richer multi-task instances are a later phase.
    /// </summary>
    public Guid? InitialTaskId { get; set; }

    /// <summary>Lifecycle status — one of "Active", "Completed", "Cancelled".</summary>
    public string Status { get; set; } = "Active";

    public DateTime? CompletedAt { get; set; }

    public FlowDefinition? WorkflowDefinition { get; set; }
    public TaskItem? InitialTask { get; set; }
}
