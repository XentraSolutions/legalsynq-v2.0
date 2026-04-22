// LSCC-010 / CC2-INT-B04 / CC2-INT-B09: Cross-service calls to the Identity service.
// CareConnect calls these during provider auto-provisioning and self-onboarding.
namespace CareConnect.Application.Interfaces;

/// <summary>
/// Thin cross-service abstraction over Identity service endpoints used during
/// provider auto-provisioning and tenant self-provisioning.
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

    // ── CC2-INT-B09: Provider tenant self-provisioning ───────────────────────

    /// <summary>
    /// Checks whether a tenant code/subdomain is available for self-provisioning.
    ///
    /// Returns null on any failure — callers should treat null as "unknown availability,
    /// proceed cautiously" (the provision step will still enforce uniqueness).
    /// </summary>
    Task<TenantCodeCheckResult?> CheckTenantCodeAvailableAsync(
        string            code,
        CancellationToken ct = default);

    /// <summary>
    /// Self-provisions a new tenant for an existing Identity user
    /// identified by <paramref name="ownerUserId"/>.
    ///
    /// NO new Identity user is created. The existing user's home TenantId is updated
    /// to the new tenant so they can log in at the new subdomain.
    ///
    /// Returns the provisioning result on success, null on any failure (caller should
    /// surface an error to the provider — this is NOT a silent fallback).
    /// </summary>
    Task<SelfProvisionTenantResult?> SelfProvisionProviderTenantAsync(
        Guid              ownerUserId,
        string            tenantName,
        string            tenantCode,
        CancellationToken ct = default);
}

// ── Result types ──────────────────────────────────────────────────────────────

public sealed class ProvisionProviderUserResult
{
    public Guid  UserId         { get; init; }
    public Guid? InvitationId   { get; init; }
    public bool  IsNew          { get; init; }
    public bool  InvitationSent { get; init; }
}

public sealed class TenantCodeCheckResult
{
    public bool    Available      { get; init; }
    public string  NormalizedCode { get; init; } = string.Empty;
    public string? Message        { get; init; }
}

public sealed class SelfProvisionTenantResult
{
    public Guid   TenantId           { get; init; }
    public string TenantCode         { get; init; } = string.Empty;
    public string Subdomain          { get; init; } = string.Empty;
    public string ProvisioningStatus { get; init; } = string.Empty;
    public string? Hostname          { get; init; }
}
