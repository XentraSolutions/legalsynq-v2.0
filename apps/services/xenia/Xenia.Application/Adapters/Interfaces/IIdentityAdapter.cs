namespace Xenia.Application.Adapters.Interfaces;

/// <summary>
/// Platform-neutral contract for identity service operations needed by Xenia.
/// Allows Xenia to validate actors and check permissions without directly importing
/// LegalSynq's Identity service models.
/// </summary>
public interface IIdentityAdapter
{
    /// <summary>Whether this adapter is configured for the current environment.</summary>
    bool IsConfigured { get; }

    /// <summary>
    /// Resolves the actor identified by <paramref name="actorId"/> within <paramref name="tenantId"/>.
    /// Returns null when the actor is not found or the adapter is unavailable.
    /// </summary>
    Task<ActorResolutionResult?> ResolveActorAsync(Guid tenantId, Guid actorId, CancellationToken ct = default);

    /// <summary>
    /// Checks whether <paramref name="actorId"/> holds the given permission within <paramref name="tenantId"/>.
    /// Returns false when the adapter is unavailable (fail-closed).
    /// </summary>
    Task<PermissionCheckResult> CheckPermissionAsync(
        Guid tenantId,
        Guid actorId,
        string permission,
        CancellationToken ct = default);
}

public sealed record ActorResolutionResult(Guid ActorId, string Email, bool IsActive);
public sealed record PermissionCheckResult(bool HasPermission, bool IsAvailable, string? Message);
