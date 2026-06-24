using Billing.Domain.Entities;

namespace Billing.Domain.Services;

/// <summary>
/// TB-DATA-01 — application service for managing the
/// <see cref="TenantBillingProfile"/> aggregate. All methods are
/// tenant-scoped: <paramref name="tenantId"/> is the scope under which the
/// operation runs (sourced from the X-Tenant-Id header at the API boundary).
/// </summary>
public interface ITenantBillingProfileService
{
    public const int DefaultPageSize = 25;
    public const int MaxPageSize     = 100;

    Task<TenantBillingProfile> CreateAsync(
        Guid tenantId,
        Guid billingAccountId,
        string? hostPlatformKey,
        string? externalTenantId,
        string mode,
        string? notes,
        CancellationToken ct = default);

    Task<TenantBillingProfile?> GetAsync(Guid tenantId, Guid profileId, CancellationToken ct = default);

    Task<TenantBillingProfile?> GetByBillingAccountAsync(
        Guid tenantId,
        Guid billingAccountId,
        CancellationToken ct = default);

    Task<TenantBillingProfilePage> ListAsync(
        Guid tenantId,
        int page,
        int pageSize,
        CancellationToken ct = default);

    Task<TenantBillingProfile> ActivateAsync(Guid tenantId, Guid profileId, CancellationToken ct = default);
    Task<TenantBillingProfile> SuspendAsync(Guid tenantId, Guid profileId, CancellationToken ct = default);
    Task<TenantBillingProfile> CloseAsync(Guid tenantId, Guid profileId, CancellationToken ct = default);
}

public sealed record TenantBillingProfilePage(
    IReadOnlyList<TenantBillingProfile> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages);
