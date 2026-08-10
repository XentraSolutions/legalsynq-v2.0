using Xenia.Application.Adapters.Interfaces;

namespace Xenia.Infrastructure.Platform;

/// <summary>
/// Noop implementation of <see cref="IIdentityAdapter"/>.
/// Returns honest unavailable results. Never reports false success.
/// </summary>
internal sealed class UnavailableIdentityAdapter : IIdentityAdapter
{
    private const string UnconfiguredMessage =
        "Identity adapter is not configured. Wire a real IIdentityAdapter to enable actor resolution.";

    public bool IsConfigured => false;

    public Task<ActorResolutionResult?> ResolveActorAsync(Guid tenantId, Guid actorId, CancellationToken ct = default)
        => Task.FromResult<ActorResolutionResult?>(null);

    public Task<PermissionCheckResult> CheckPermissionAsync(
        Guid tenantId,
        Guid actorId,
        string permission,
        CancellationToken ct = default)
        => Task.FromResult(new PermissionCheckResult(
            HasPermission: false,
            IsAvailable: false,
            Message: UnconfiguredMessage));
}
