namespace Tenant.Application.Interfaces;

/// <summary>
/// TENANT-B12 — Adapter that orchestrates Identity-side provisioning work
/// after the Tenant service has created the canonical Tenant record.
///
/// Responsibilities:
///   - Create the Identity.Tenant entity (using the Tenant-service-generated ID)
///   - Create the default admin Organization, User, Membership, and RoleAssignment
///   - Trigger DNS/subdomain provisioning
///   - Trigger product provisioning (if products specified)
///   - Return a structured result with admin credentials and provisioning outcome
///
/// Rules:
///   - HTTP/internal service calls only — no direct DB access
///   - 3 s timeout; failures return IdentityProvisioningResult with Success=false
///   - Never throws — always returns a result
/// </summary>
public interface IIdentityProvisioningAdapter
{
    /// <summary>
    /// Calls the Identity internal provisioning endpoint to create the auth/admin
    /// context for a tenant that already exists in the Tenant service DB.
    /// </summary>
    Task<IdentityProvisioningResult> ProvisionAsync(
        IdentityProvisioningRequest request,
        CancellationToken ct = default);
}

/// <summary>Inputs required to provision a tenant's Identity-side context.</summary>
public record IdentityProvisioningRequest(
    Guid     TenantId,
    string   Code,
    string   DisplayName,
    string   OrgType,
    string   AdminEmail,
    string   AdminFirstName,
    string   AdminLastName,
    string?  PreferredSubdomain    = null,
    string?  AddressLine1          = null,
    string?  City                  = null,
    string?  State                 = null,
    string?  PostalCode            = null,
    double?  Latitude              = null,
    double?  Longitude             = null,
    string?  GeoPointSource        = null,
    List<string>? Products         = null);

/// <summary>Result returned by the Identity provisioning adapter.</summary>
public record IdentityProvisioningResult(
    bool     Success,
    string?  AdminUserId,
    string?  AdminEmail,
    string?  TemporaryPassword,
    string?  ProvisioningStatus,
    string?  Hostname,
    string?  Subdomain,
    List<string> Warnings,
    List<string> Errors);
