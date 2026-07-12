namespace Xenia.Application.Adapters.Interfaces;

/// <summary>
/// Platform-neutral contract for submitting workflow triggers to the
/// platform's workflow execution engine.
///
/// Xenia modules may trigger downstream workflows; this adapter decouples
/// Xenia from the specific workflow engine implementation.
/// </summary>
public interface IWorkflowAdapter
{
    /// <summary>Whether this adapter is configured for the current environment.</summary>
    bool IsConfigured { get; }

    /// <summary>
    /// Submits a workflow trigger to the platform workflow service.
    /// Returns a trigger reference if accepted, or an unavailable result.
    /// </summary>
    Task<WorkflowTriggerResult> SubmitTriggerAsync(
        WorkflowTriggerRequest request,
        CancellationToken ct = default);
}

public sealed record WorkflowTriggerRequest(
    Guid TenantId,
    string WorkflowKey,
    string? CorrelationId,
    IReadOnlyDictionary<string, object?> Payload);

public sealed record WorkflowTriggerResult(bool IsSubmitted, bool IsAvailable, string? TriggerId, string? Message);
