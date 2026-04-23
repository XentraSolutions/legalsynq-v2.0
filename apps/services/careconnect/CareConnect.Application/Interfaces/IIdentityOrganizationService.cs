// LSCC-010 / CC2-INT-B04: Cross-service calls to the Identity service.
// Identity service = membership / access only (BLK-CC-01).
//
// Retired methods (BLK-ID-01 → now use ITenantServiceClient + IIdentityMembershipClient):
//   - CheckTenantCodeAvailableAsync  (was GET /api/admin/tenants/check-code)
//   - SelfProvisionProviderTenantAsync (was POST /api/admin/tenants/self-provision)
namespace CareConnect.Application.Interfaces;

/// <summary>
/// Thin cross-service abstraction over Identity service endpoints used during
/// provider auto-provisioning and user invitation.
///
/// Scope (BLK-CC-01): Identity = org creation + user invitation ONLY.
/// Tenant lifecycle (check-code, provision) is handled by ITenantServiceClient.
/// Tenant membership (assign-tenant, assign-roles) is handled by IIdentityMembershipClient.
/// </summary>
public interface IIdentityOrganizationService
{
    // ── LSCC-010: Provider org creation ──────────────────────────────────────

    /// <summary>
    /// Creates or resolves a minimal PROVIDER Organization in the Identity service
    /// for the given CareConnect provider.
    ///
    /// Idempotency: the identity endpoint uses (TenantId + ProviderCcId) as the
    /// unique key. Repeated calls with the same inputs return the same org ID.
    ///
    /// Returns the Identity OrganizationId on success, null on any failure.
    /// Callers must treat null as "fall back to LSCC-009".
    /// </summary>
    Task<Guid?> EnsureProviderOrganizationAsync(
        Guid              tenantId,
        Guid              providerCcId,
        string            providerName,
        CancellationToken ct = default);

    // ── CC2-INT-B04: Token → Identity Bridge — user invitation ───────────────

    /// <summary>
    /// Creates an inactive Identity user under the given org's tenant and sends
    /// them an invitation email so they can set a password and log in.
    ///
    /// Idempotent: if a user with the given email already exists in the org's tenant
    /// the existing user record is returned and no duplicate is created.
    ///
    /// Returns a result on success (isNew=true for new users, false for existing).
    /// Returns null on any failure — non-fatal; provider org link is already established.
    /// </summary>
    Task<ProvisionProviderUserResult?> InviteProviderUserAsync(
        Guid              orgId,
        string            email,
        string            firstName,
        string?           lastName,
        CancellationToken ct = default);
}

// ── Result types ───────────────────────────────────────────────────────────────

public sealed class ProvisionProviderUserResult
{
    public Guid  UserId         { get; init; }
    public Guid? InvitationId   { get; init; }
    public bool  IsNew          { get; init; }
    public bool  InvitationSent { get; init; }
}
