namespace BuildingBlocks.FlowClient;

/// <summary>
/// LS-FLOW-MERGE-P4 — shared HTTP adapter that product services use to
/// integrate with the Flow workflow engine. Pass-through bearer auth, retry,
/// and timeout are handled by the implementation.
///
/// <para>
/// <c>productSlug</c> is one of <c>"synqlien" | "careconnect" | "synqfund"</c>
/// and selects which Flow capability policy gates the call (the underlying
/// route segment matches Flow's <c>ProductWorkflowsController</c>).
/// </para>
/// </summary>
public interface IFlowClient
{
    Task<FlowProductWorkflowResponse> StartWorkflowAsync(
        string productSlug,
        StartProductWorkflowRequest request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<FlowProductWorkflowResponse>> ListBySourceEntityAsync(
        string productSlug,
        string sourceEntityType,
        string sourceEntityId,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Raised when a Flow call cannot be completed because the upstream service
/// is unreachable, timed out, or returned an unexpected response. Callers
/// should map this to HTTP 503 — the local request itself is healthy, but
/// the integration is degraded.
/// </summary>
public sealed class FlowClientUnavailableException : Exception
{
    public FlowClientUnavailableException(string message, Exception? inner = null)
        : base(message, inner) { }
}
