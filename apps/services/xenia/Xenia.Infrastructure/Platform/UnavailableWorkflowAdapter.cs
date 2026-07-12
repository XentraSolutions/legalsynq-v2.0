using Xenia.Application.Adapters.Interfaces;

namespace Xenia.Infrastructure.Platform;

/// <summary>
/// Noop implementation of <see cref="IWorkflowAdapter"/>.
/// Returns honest unavailable results. Never reports false success.
/// </summary>
internal sealed class UnavailableWorkflowAdapter : IWorkflowAdapter
{
    private const string UnconfiguredMessage =
        "Workflow adapter is not configured. Wire a real IWorkflowAdapter for production.";

    public bool IsConfigured => false;

    public Task<WorkflowTriggerResult> SubmitTriggerAsync(
        WorkflowTriggerRequest request, CancellationToken ct = default)
        => Task.FromResult(new WorkflowTriggerResult(
            IsSubmitted: false,
            IsAvailable: false,
            TriggerId: null,
            Message: UnconfiguredMessage));
}
