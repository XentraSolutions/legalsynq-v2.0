// LSCC-010 / CC2-INT-B04: Cross-service calls to the Identity service for provider provisioning.
// CareConnect calls these during auto-provisioning to:
//   1. Create/resolve the Identity Organization linked to the provider record.
//   2. Create an Identity user + send an invitation email to the activating person.
namespace CareConnect.Application.Interfaces;

/// <summary>
/// Thin cross-service abstraction over Identity service endpoints used during
/// provider auto-provisioning. Returns null on any failure — all failures trigger
/// LSCC-009 queue fallback (for org creation) or are non-fatal warnings (for user invitation).
/// </summary>
public interface IIdentityOrganizationService
{
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

    /// <summary>
    /// CC2-INT-B04 — Token → Identity Bridge.
    ///
    /// Creates an inactive Identity user under the given org's tenant and sends
    /// them an invitation email so they can set a password and log in.
    ///
    /// Idempotent: if a user with the given email already exists in the org's tenant
    /// the existing user record is returned and no duplicate is created.
    ///
    /// Returns a result on success (isNew=true for new users, false for existing).
    /// Returns null on any failure — the caller logs a warning but does NOT fail the
    /// overall provision flow. The provider org link is already established at this
    /// point, so losing the invitation is recoverable (admin can resend from Identity).
    /// </summary>
    Task<ProvisionProviderUserResult?> InviteProviderUserAsync(
        Guid              orgId,
        string            email,
        string            firstName,
        string?           lastName,
        CancellationToken ct = default);
}

/// <summary>
/// Result of a successful call to IIdentityOrganizationService.InviteProviderUserAsync.
/// </summary>
public sealed class ProvisionProviderUserResult
{
    public Guid  UserId         { get; init; }
    public Guid? InvitationId   { get; init; }
    public bool  IsNew          { get; init; }
    public bool  InvitationSent { get; init; }
}
