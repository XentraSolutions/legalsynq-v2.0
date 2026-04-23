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
    /// TENANT-B07 — Dual-write upsert from Identity sync event.
    /// Creates the tenant if it does not yet exist in Tenant service;
    /// updates it if it does. Idempotent and safe to call multiple times.
    /// </summary>
    Task UpsertFromSyncAsync(TenantSyncRequest request, CancellationToken ct = default);
}
