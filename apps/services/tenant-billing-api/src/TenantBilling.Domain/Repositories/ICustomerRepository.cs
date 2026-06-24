using TenantBilling.Domain.Entities;

namespace TenantBilling.Domain.Repositories;

public interface ICustomerRepository
{
    Task<Customer> AddAsync(Customer customer, CancellationToken ct = default);

    Task<Customer> UpdateAsync(Customer customer, CancellationToken ct = default);

    /// <summary>
    /// Tenant-scoped lookup. Returns the customer regardless of soft-delete state
    /// (use <see cref="GetActiveByIdAsync"/> to filter out deleted records). Returns
    /// <c>null</c> for cross-tenant or unknown ids so callers map to a uniform 404
    /// without leaking existence.
    /// </summary>
    Task<Customer?> GetByIdAsync(Guid tenantId, Guid customerId, CancellationToken ct = default);

    /// <summary>
    /// Tenant-scoped lookup that excludes soft-deleted customers. Returns <c>null</c>
    /// for cross-tenant, soft-deleted, or unknown ids.
    /// </summary>
    Task<Customer?> GetActiveByIdAsync(Guid tenantId, Guid customerId, CancellationToken ct = default);

    /// <summary>
    /// Tenant-scoped, paginated list. Excludes soft-deleted records. <paramref name="search"/>
    /// is matched case-insensitively against Name, Email, Phone, and ExternalReference.
    /// Newest first.
    /// </summary>
    Task<IReadOnlyList<Customer>> ListAsync(
        Guid tenantId,
        string? search,
        int page,
        int pageSize,
        CancellationToken ct = default);

    /// <summary>
    /// Total active customers matching the same filter as <see cref="ListAsync"/>.
    /// </summary>
    Task<int> CountAsync(Guid tenantId, string? search, CancellationToken ct = default);

    /// <summary>
    /// True when an active customer in the same tenant already uses the given
    /// email (case-insensitive). When <paramref name="excludingCustomerId"/> is
    /// provided that customer is ignored (used for update operations).
    /// </summary>
    Task<bool> ExistsByTenantAndEmailAsync(
        Guid tenantId,
        string email,
        Guid? excludingCustomerId = null,
        CancellationToken ct = default);
}
