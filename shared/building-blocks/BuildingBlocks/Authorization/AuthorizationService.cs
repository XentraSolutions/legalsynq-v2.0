using BuildingBlocks.Context;

namespace BuildingBlocks.Authorization;

/// <summary>
/// Thin wrapper that applies the PlatformAdmin bypass before delegating
/// capability checks to <see cref="ICapabilityService"/>.
/// </summary>
public sealed class AuthorizationService
{
    private readonly ICapabilityService _caps;

    public AuthorizationService(ICapabilityService caps) => _caps = caps;

    /// <summary>
    /// Returns true when the user is a PlatformAdmin (unconditional bypass)
    /// or when the capability service confirms the user holds the required capability
    /// through their product roles.
    /// </summary>
    public async Task<bool> IsAuthorizedAsync(
        ICurrentRequestContext ctx,
        string capabilityCode,
        CancellationToken ct = default)
    {
        if (ctx.IsPlatformAdmin) return true;
        return await _caps.HasCapabilityAsync(ctx.ProductRoles, capabilityCode, ct);
    }

    /// <summary>
    /// Throws <see cref="global::BuildingBlocks.Exceptions.ForbiddenException"/>
    /// when the user lacks the required capability.
    /// </summary>
    public async Task RequireCapabilityAsync(
        ICurrentRequestContext ctx,
        string capabilityCode,
        CancellationToken ct = default)
    {
        if (!await IsAuthorizedAsync(ctx, capabilityCode, ct))
            throw new global::BuildingBlocks.Exceptions.ForbiddenException(capabilityCode);
    }
}
