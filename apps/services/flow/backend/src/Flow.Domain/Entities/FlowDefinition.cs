using Flow.Domain.Common;
using Flow.Domain.Enums;

namespace Flow.Domain.Entities;

public class FlowDefinition : AuditableEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Version { get; set; } = "1.0";
    public FlowStatus Status { get; set; } = FlowStatus.Draft;

    /// <summary>
    /// LS-FLOW-020-A — Product Context Layer. Required, validated against
    /// <see cref="ProductKeys.All"/>. Existing rows backfilled to
    /// <see cref="ProductKeys.FlowGeneric"/>.
    /// </summary>
    public string ProductKey { get; set; } = ProductKeys.FlowGeneric;

    public ICollection<WorkflowStage> Stages { get; set; } = new List<WorkflowStage>();
    public ICollection<WorkflowTransition> Transitions { get; set; } = new List<WorkflowTransition>();
    public ICollection<WorkflowAutomationHook> AutomationHooks { get; set; } = new List<WorkflowAutomationHook>();
}
