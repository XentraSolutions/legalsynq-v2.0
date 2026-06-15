using Tenant.Application.DTOs;

namespace Tenant.Application.Interfaces;

public interface ITenantService
{
    Task<TenantResponse?>                           GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<TenantResponse?>                           GetByCodeAsync(string code, CancellationToken ct = default);
    Task<(List<TenantResponse> Items, int Total)>   ListAsync(int page, int pageSize, CancellationToken ct = default);
    Task<TenantResponse>                            CreateAsync(CreateTenantRequest request, CancellationToken ct = default);
    Task<TenantResponse>                            UpdateAsync(Guid id, UpdateTenantRequest request, CancellationToken ct = default);
    Task                                            DeactivateAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// BLK-TS-01 — Validate format and check uniqueness of a tenant code.
    /// Returns availability without creating any record.
    /// </summary>
    Task<CheckCodeResponse>  CheckCodeAsync(string code, CancellationToken ct = default);

    /// <summary>
    /// BLK-TS-01 — Minimal provision: create a tenant from name + code only.
    /// Subdomain defaults to the normalized code.
    /// Does NOT create users, memberships, or provision DNS.
    /// </summary>
    Task<ProvisionResponse>  ProvisionAsync(ProvisionRequest request, CancellationToken ct = default);

    /// <summary>
    /// TENANT-B07 — Dual-write upsert from Identity sync event.
    /// Creates the tenant if it does not yet exist in Tenant service;
    /// updates it if it does. Idempotent and safe to call multiple times.
    /// </summary>
    Task UpsertFromSyncAsync(TenantSyncRequest request, CancellationToken ct = default);

    /// <summary>
    /// Resolves the effective timezone for a tenant from the canonical Tenant row,
    /// then compatible settings rows, then the application default.
    /// </summary>
    Task<string> GetTimezoneAsync(Guid tenantId, CancellationToken ct = default);

    /// <summary>
    /// Updates only the IANA timezone for a tenant.
    /// Accepts any value resolvable by TimeZoneInfo.FindSystemTimeZoneById.
    /// </summary>
    Task<string> UpdateTimezoneAsync(Guid tenantId, string timezone, CancellationToken ct = default);
}
