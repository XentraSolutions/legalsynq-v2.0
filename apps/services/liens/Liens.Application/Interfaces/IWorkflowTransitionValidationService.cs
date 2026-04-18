namespace Liens.Application.Interfaces;

/// <summary>
/// LS-LIENS-FLOW-005 — Reusable transition validation.
/// Used by task, case, and lien stage-movement operations.
/// </summary>
public interface IWorkflowTransitionValidationService
{
    /// <summary>
    /// Returns true if the workflow has no configured active transitions at all
    /// (open-move mode) OR if an active transition from→to exists.
    /// Returns false if transitions are configured but the specific from→to is not allowed.
    /// </summary>
    Task<bool> IsTransitionAllowedAsync(
        Guid workflowConfigId,
        Guid fromStageId,
        Guid toStageId,
        CancellationToken ct = default);

    /// <summary>
    /// Returns all ToStageIds reachable from the given stage in this workflow.
    /// Returns an empty list when no transitions are configured (open-move mode).
    /// </summary>
    Task<IReadOnlyList<Guid>> GetAllowedNextStagesAsync(
        Guid workflowConfigId,
        Guid fromStageId,
        CancellationToken ct = default);
}
