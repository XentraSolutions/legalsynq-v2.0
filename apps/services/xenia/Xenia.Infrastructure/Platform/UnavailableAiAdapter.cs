using Xenia.Application.Adapters.Interfaces;

namespace Xenia.Infrastructure.Platform;

/// <summary>
/// Noop implementation of <see cref="IAiAdapter"/>.
/// Returns honest unavailable results. Never reports false success.
/// </summary>
internal sealed class UnavailableAiAdapter : IAiAdapter
{
    private const string UnconfiguredMessage =
        "AI adapter is not configured. Wire a real IAiAdapter for production.";

    public bool IsConfigured => false;

    public Task<AiProcessingResult> SubmitProcessingRequestAsync(
        AiProcessingRequest request, CancellationToken ct = default)
        => Task.FromResult(new AiProcessingResult(
            IsSubmitted: false,
            IsAvailable: false,
            RequestId: null,
            Message: UnconfiguredMessage));
}
