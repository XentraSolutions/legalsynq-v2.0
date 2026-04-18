using Liens.Application.Interfaces;
using Liens.Application.Repositories;

namespace Liens.Application.Services;

/// <summary>
/// LS-LIENS-FLOW-005 — Runtime transition validation.
/// Reusable by task, case, and lien stage-movement operations.
/// </summary>
public sealed class WorkflowTransitionValidationService : IWorkflowTransitionValidationService
{
    private readonly ILienWorkflowConfigRepository _repo;

    public WorkflowTransitionValidationService(ILienWorkflowConfigRepository repo)
        => _repo = repo;

    public async Task<bool> IsTransitionAllowedAsync(
        Guid workflowConfigId,
        Guid fromStageId,
        Guid toStageId,
        CancellationToken ct = default)
    {
        if (fromStageId == toStageId) return false;

        var transitions = await _repo.GetActiveTransitionsAsync(workflowConfigId, ct);

        // If no transitions are configured at all, open-move mode — allow everything
        if (transitions.Count == 0) return true;

        return transitions.Any(t => t.FromStageId == fromStageId && t.ToStageId == toStageId);
    }

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
