namespace Flow.Domain.Interfaces;

/// <summary>
/// LS-FLOW-MERGE-P3 — Application-level access to the current authenticated
/// user / tenant. Defined in Flow.Domain so application services can be
/// independent of the BuildingBlocks shared library; implemented in Flow.Api
/// on top of <c>BuildingBlocks.Context.ICurrentRequestContext</c>.
/// </summary>
public interface IFlowUserContext
{
    /// <summary>Tenant id formatted exactly as <see cref="ITenantProvider.GetTenantId"/> returns it.</summary>
    string? TenantId { get; }

    /// <summary>Authenticated user id (string form), or null when anonymous.</summary>
    string? UserId { get; }
}
