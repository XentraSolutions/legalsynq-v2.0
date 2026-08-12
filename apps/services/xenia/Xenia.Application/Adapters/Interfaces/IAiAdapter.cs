namespace Xenia.Application.Adapters.Interfaces;

/// <summary>
/// Platform-neutral contract for submitting AI processing requests.
///
/// This adapter abstracts the AI inference infrastructure (e.g. OpenAI,
/// Azure OpenAI, a platform-managed inference service) from Xenia modules.
/// </summary>
public interface IAiAdapter
{
    /// <summary>Whether this adapter is configured for the current environment.</summary>
    bool IsConfigured { get; }

    /// <summary>
    /// Submits a processing request to the platform AI service.
    /// Returns a processing reference if accepted, or an unavailable result.
    /// </summary>
    Task<AiProcessingResult> SubmitProcessingRequestAsync(
        AiProcessingRequest request,
        CancellationToken ct = default);
}

public sealed record AiProcessingRequest(
    Guid TenantId,
    string ModelKey,
    string Prompt,
    IReadOnlyDictionary<string, object?> Parameters,
    string? CorrelationId = null);

public sealed record AiProcessingResult(bool IsSubmitted, bool IsAvailable, string? RequestId, string? Message);
