namespace CareConnect.Application.DTOs;

// CC2-INT-B06 — request/response DTOs for provider network management

// ── List / Summary ────────────────────────────────────────────────────────────

public sealed record NetworkSummaryResponse(
    Guid   Id,
    string Name,
    string Description,
    int    ProviderCount,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);

// ── Detail ────────────────────────────────────────────────────────────────────

public sealed record NetworkDetailResponse(
    Guid   Id,
    string Name,
    string Description,
    List<NetworkProviderItem> Providers,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);

public sealed record NetworkProviderItem(
    Guid   Id,
    string Name,
    string? OrganizationName,
    string Email,
    string Phone,
    string City,
    string State,
    bool   IsActive,
    bool   AcceptingReferrals);

// ── Map markers ───────────────────────────────────────────────────────────────

public sealed record NetworkProviderMarker(
    Guid   Id,
    string Name,
    string? OrganizationName,
    string City,
    string State,
    string AddressLine1,
    string PostalCode,
    string Email,
    string Phone,
    bool   AcceptingReferrals,
    bool   IsActive,
    double Latitude,
    double Longitude,
    string? GeoPointSource);

// ── Mutations ─────────────────────────────────────────────────────────────────

public sealed record CreateNetworkRequest(
    string Name,
    string Description);

public sealed record UpdateNetworkRequest(
    string Name,
    string Description);
