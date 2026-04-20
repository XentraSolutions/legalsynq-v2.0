namespace Liens.Application.Interfaces;

/// <summary>
/// LS-LIENS-FLOW-005 — My Tasks stage-transition validation.
///
/// This service governs task-stage movement only — it controls which workflow stages
/// a LienTask is allowed to move between inside the My Tasks module.
///
/// It does NOT govern case or lien workflow instance transitions.
/// Case/lien workflow execution is owned by the Flow service (IFlowClient) and operates
/// independently of this validation layer.
///
/// Transitional architecture note (LS-LIENS-FLOW-007):
/// A future version of this interface will accept an optional Flow instance context
/// so that task-stage transitions can be validated against (or enriched by) the active
/// Flow workflow instance state for the linked case.
/// </summary>
public interface IWorkflowTransitionValidationService
{
    /// <summary>
    /// Returns true if the task-stage move from <paramref name="fromStageId"/> to
    /// <paramref name="toStageId"/> is permitted by the workflow configuration.
    ///
    /// Open-move mode: when no active transitions are configured, any task-stage movement is allowed.
    /// Strict mode: when transitions are configured, only explicitly defined from→to pairs are permitted.
    ///
    /// This validates My Tasks stage movement only.
    /// It does not validate Flow service case/lien workflow instance transitions.
    ///
    /// LS-LIENS-FLOW-007 readiness: a future overload will accept
    /// Guid? flowInstanceId to optionally correlate with the active Flow instance.
    /// </summary>
    Task<bool> IsTransitionAllowedAsync(
        Guid workflowConfigId,
        Guid fromStageId,
        Guid toStageId,
        CancellationToken ct = default);

    /// <summary>
    /// Returns all task stage IDs that a task is allowed to move to from <paramref name="fromStageId"/>.
    /// Returns an empty list when in open-move mode (no transitions configured).
    ///
    /// Scope: My Tasks stage transitions only.
    /// </summary>
    Task<IReadOnlyList<Guid>> GetAllowedNextStagesAsync(
        Guid workflowConfigId,
        Guid fromStageId,
        CancellationToken ct = default);
}
