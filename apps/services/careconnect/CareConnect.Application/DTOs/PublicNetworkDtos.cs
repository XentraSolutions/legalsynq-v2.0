namespace CareConnect.Application.DTOs;

// CC2-INT-B07 — Public network surface DTOs.
// These are safe to expose without authentication.
// Tenant ID is NEVER included in responses (the caller already knows which tenant they're on).

/// <summary>
/// Public-facing network summary.
/// Returned by GET /api/public/network — accessible without authentication.
/// </summary>
public sealed record PublicNetworkSummary(
    Guid   Id,
    string Name,
    string Description,
    int    ProviderCount);

/// <summary>
/// Public-facing provider item within a network.
/// Omits sensitive internal IDs; safe to return to unauthenticated callers.
/// </summary>
public sealed record PublicProviderItem(
    Guid    Id,
    string  Name,
    string? OrganizationName,
    string  Phone,
    string  City,
    string  State,
    string  PostalCode,
    bool    IsActive,
    bool    AcceptingReferrals,
    string  AccessStage,
    string? PrimaryCategory);

/// <summary>
/// Public-facing map marker for a provider in a network.
/// Latitude/Longitude included only when the provider has geo data.
/// </summary>
public sealed record PublicProviderMarker(
    Guid    Id,
    string  Name,
    string? OrganizationName,
    string  City,
    string  State,
    bool    AcceptingReferrals,
    double  Latitude,
    double  Longitude);

/// <summary>
/// Resolved public network surface returned when the tenant has a single network.
/// Bundles the network + its providers for a single API round-trip.
/// </summary>
public sealed record PublicNetworkDetail(
    Guid   NetworkId,
    string NetworkName,
    string NetworkDescription,
    List<PublicProviderItem>   Providers,
    List<PublicProviderMarker> Markers);

/// <summary>
/// Stage-based redirect instruction returned when the network surface detects
/// a provider/user should be redirected to a more advanced portal.
/// CC2-INT-B06-02 stage enforcement for the public surface.
/// </summary>
public sealed record StageRedirectInfo(
    string Stage,
    string TargetUrl);
