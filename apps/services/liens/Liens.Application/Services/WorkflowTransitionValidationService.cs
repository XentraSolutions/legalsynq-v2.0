using Liens.Application.Interfaces;
using Liens.Application.Repositories;

namespace Liens.Application.Services;

/// <summary>
/// LS-LIENS-FLOW-005 — My Tasks stage-transition validation service.
///
/// Architectural boundary:
///   - This service governs task-stage movement within the My Tasks module only.
///   - It does NOT govern case or lien workflow instance transitions.
///   - Case/lien workflow instance execution is owned by the Flow service (IFlowClient).
///     Flow manages WorkflowInstances via StartWorkflow / AdvanceWorkflow / CompleteWorkflow.
///     That logic lives in WorkflowEndpoints.cs and is entirely separate from this service.
///
/// Transitional note (LS-LIENS-FLOW-007):
///   The current transition check is purely Liens-local (LienWorkflowTransition table).
///   In LS-LIENS-FLOW-007, this service will be extended to optionally accept a
///   Flow instance context (flowInstanceId) so task-stage validation can be correlated
///   with the active Flow workflow state for the task's linked case.
///   No such correlation is implemented here — this is the preparation seam.
/// </summary>
public sealed class WorkflowTransitionValidationService : IWorkflowTransitionValidationService
{
    private readonly ILienWorkflowConfigRepository _repo;

    public WorkflowTransitionValidationService(ILienWorkflowConfigRepository repo)
        => _repo = repo;

    /// <inheritdoc />
    public async Task<bool> IsTransitionAllowedAsync(
        Guid workflowConfigId,
        Guid fromStageId,
        Guid toStageId,
        CancellationToken ct = default)
    {
        if (fromStageId == toStageId) return false;

        var transitions = await _repo.GetActiveTransitionsAsync(workflowConfigId, ct);

        // Open-move mode: no transitions configured → allow any task-stage movement.
        // This is intentional — tenants can operate without strict transition rules.
        if (transitions.Count == 0) return true;

        // Strict mode: at least one transition is configured → only explicitly allowed moves pass.
        // LS-LIENS-FLOW-007: future integration point — optionally validate against
        // the active Flow WorkflowInstance state for the task's linked case here.
        return transitions.Any(t => t.FromStageId == fromStageId && t.ToStageId == toStageId);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Guid>> GetAllowedNextStagesAsync(
        Guid workflowConfigId,
        Guid fromStageId,
        CancellationToken ct = default)
    {
        var transitions = await _repo.GetActiveTransitionsAsync(workflowConfigId, ct);
        return transitions
            .Where(t => t.FromStageId == fromStageId)
            .Select(t => t.ToStageId)
            .ToList();
    }
}
