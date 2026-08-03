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
    string? Title,
    string? OrganizationName,
    string Email,
    string Phone,
    string City,
    string State,
    string AddressLine1,
    string PostalCode,
    bool   IsActive,
    bool   AcceptingReferrals,
    string AccessStage,
    List<SpecialtyResponse> Specialties,
    Guid? PrimarySpecialtyId,
    string? PrimarySpecialty);

// ── Map markers ───────────────────────────────────────────────────────────────

public sealed record NetworkProviderMarker(
    Guid   Id,
    string Name,
    string? Title,
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
    string? GeoPointSource,
    List<SpecialtyResponse> Specialties,
    Guid? PrimarySpecialtyId,
    string? PrimarySpecialty);

// ── Shared provider search ────────────────────────────────────────────────────

/// <summary>Returned by GET /api/networks/{id}/providers/search</summary>
public sealed record ProviderSearchResult(
    Guid    Id,
    string  Name,
    string? Title,
    string? OrganizationName,
    string  Email,
    string  Phone,
    string  City,
    string  State,
    string  AddressLine1,
    string  PostalCode,
    string? Npi,
    bool    IsActive,
    bool    AcceptingReferrals,
    string  AccessStage,
    List<SpecialtyResponse> Specialties,
    Guid? PrimarySpecialtyId,
    string? PrimarySpecialty);

// ── Mutations ─────────────────────────────────────────────────────────────────

public sealed record CreateNetworkRequest(
    string Name,
    string Description);

public sealed record UpdateNetworkRequest(
    string Name,
    string Description);

/// <summary>
/// CC2-INT-B06-01: Add a provider to a network.
/// Exactly one of ExistingProviderId or NewProvider must be set.
/// - ExistingProviderId → associate existing shared provider (no new record)
/// - NewProvider        → create in shared registry then associate
/// </summary>
public sealed record AddProviderToNetworkRequest(
    Guid?                  ExistingProviderId,
    NewProviderData?       NewProvider);

public sealed record NewProviderData(
    string  FirstName,
    string  LastName,
    string? OrganizationName,
    string  Email,
    string  Phone,
    string  AddressLine1,
    string  City,
    string  State,
    string  PostalCode,
    bool    IsActive,
    bool    AcceptingReferrals,
    string? Npi,
    /// <summary>Category codes (e.g. "IMG", "PAIN"). The primary category code should be first.</summary>
    List<string>? CategoryCodes = null,
    /// <summary>The code of the default/primary provider type (must be present in CategoryCodes).</summary>
    string? PrimaryCategoryCode = null,
    /// <summary>Specialty codes (e.g. "PHYSICAL_THERAPY"). The primary specialty code should be first.</summary>
    List<string>? SpecialtyCodes = null,
    /// <summary>The code of the default/primary specialty (must be present in SpecialtyCodes).</summary>
    string? PrimarySpecialtyCode = null,
    double? Latitude = null,
    double? Longitude = null,
    string? GeoPointSource = null,
    string? Title = null);

public sealed record UpdateNetworkProviderRequest(
    string  FirstName,
    string  LastName,
    string? OrganizationName,
    string  Email,
    string  Phone,
    string  AddressLine1,
    string  City,
    string  State,
    string  PostalCode,
    bool    IsActive,
    bool    AcceptingReferrals,
    List<Guid> SpecialtyIds,
    double? Latitude = null,
    double? Longitude = null,
    string? GeoPointSource = null,
    string? Title = null);

// ── Provider import ──────────────────────────────────────────────────────────

public sealed record ProviderImportParsedRow(
    int     RowNumber,
    string  SourceKey,
    string? TenantId,
    string? FirstName,
    string? LastName,
    string? OrganizationName,
    string? Npi,
    string? Email,
    string? Phone,
    string? AddressLine1,
    string? City,
    string? State,
    string? PostalCode,
    string? IsActiveRaw,
    string? AcceptingReferralsRaw);

public sealed record ProviderImportNormalizedRow(
    Guid    TenantId,
    string  FirstName,
    string  LastName,
    string? OrganizationName,
    string? Npi,
    string  Email,
    string  Phone,
    string  AddressLine1,
    string  City,
    string  State,
    string  PostalCode,
    bool    IsActive,
    bool    AcceptingReferrals);

public sealed record ProviderImportParseResult(
    string FileName,
    int TotalRows,
    List<ProviderImportParsedRow> Rows);

public sealed record ProviderImportRowResult(
    int                          RowNumber,
    string                       SourceKey,
    string                       Status,
    Guid?                        ProviderId,
    string                       Message,
    ProviderImportNormalizedRow? NormalizedProvider,
    List<string>                 Errors);

public sealed record ProviderImportSummaryResponse(
    Guid                          TenantId,
    Guid                          NetworkId,
    string                        FileName,
    bool                          DryRun,
    int                           TotalRows,
    int                           ValidRows,
    int                           ProcessedRows,
    int                           CreatedProviders,
    int                           ReusedByNpi,
    int                           ReusedByEmail,
    int                           AlreadyInNetwork,
    int                           FailedRows,
    List<ProviderImportRowResult> Rows);
