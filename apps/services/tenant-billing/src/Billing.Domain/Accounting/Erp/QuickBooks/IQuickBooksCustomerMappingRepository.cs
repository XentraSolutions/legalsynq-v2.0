namespace Billing.Domain.Accounting.Erp.QuickBooks;

/// <summary>
/// MS-BILL-ERP-003 — Tenant-scoped persistence contract for
/// <see cref="QuickBooksCustomerMapping"/>.
///
/// <para>
/// Every method accepts an explicit <c>tenantId</c> as the first
/// argument; the implementation MUST apply
/// <c>Where(x =&gt; x.TenantId == tenantId)</c> at the SQL level so
/// cross-tenant probes silently return <c>null</c> / empty rather
/// than 403.
/// </para>
///
/// <para>
/// Uniqueness invariants enforced by the SQL schema (and surfaced
/// here as <see cref="QuickBooksCustomerMappingConflictException"/>):
/// </para>
/// <list type="bullet">
///   <item>One mapping per (TenantId, BillingCustomerId).</item>
///   <item>One mapping per (TenantId, QuickBooksCustomerId).</item>
/// </list>
/// </summary>
public interface IQuickBooksCustomerMappingRepository
{
    Task<QuickBooksCustomerMapping> AddAsync(
        QuickBooksCustomerMapping mapping,
        CancellationToken ct = default);

    Task UpdateAsync(
        QuickBooksCustomerMapping mapping,
        CancellationToken ct = default);

    Task<bool> DeleteAsync(
        Guid tenantId,
        Guid id,
        CancellationToken ct = default);

    Task<QuickBooksCustomerMapping?> GetByIdAsync(
        Guid tenantId,
        Guid id,
        CancellationToken ct = default);

    /// <summary>
    /// Resolver entry point: look up the mapping for a given
    /// Billing customer. Returns the row regardless of
    /// <see cref="QuickBooksCustomerMapping.MappingStatus"/> —
    /// callers (the resolver service) inspect status to decide
    /// whether to treat <c>Disabled</c> as "no mapping".
    /// </summary>
    Task<QuickBooksCustomerMapping?> GetByBillingCustomerAsync(
        Guid tenantId,
        Guid billingCustomerId,
        CancellationToken ct = default);

    Task<IReadOnlyList<QuickBooksCustomerMapping>> ListAsync(
        Guid tenantId,
        int page,
        int pageSize,
        CancellationToken ct = default);

    /// <summary>
    /// MS-BILL-ERP-005: direct tenant-scoped lookup by
    /// <c>(TenantId, QuickBooksCustomerId)</c>. Used by the
    /// remediation validate probe to detect QBO-side conflicts
    /// without paging through every mapping in the tenant. The
    /// implementation MUST hit the tenant-scoped unique index on
    /// <c>(TenantId, QuickBooksCustomerId)</c>. Returns the row
    /// regardless of <see cref="QuickBooksCustomerMapping.MappingStatus"/>;
    /// the caller decides how to interpret a Disabled row.
    /// </summary>
    Task<QuickBooksCustomerMapping?> GetByQuickBooksCustomerIdAsync(
        Guid tenantId,
        string quickBooksCustomerId,
        CancellationToken ct = default);

    /// <summary>
    /// Stamp <see cref="QuickBooksCustomerMapping.LastExportedAtUtc"/>
    /// after a successful provider call. Best-effort; failure to
    /// stamp MUST NOT roll back or fail the export — the audit
    /// signal is informational only.
    /// </summary>
    Task TouchLastExportedAsync(
        Guid tenantId,
        Guid id,
        DateTime nowUtc,
        CancellationToken ct = default);
}

/// <summary>
/// Raised by the repository when a write violates one of the
/// unique-index invariants on (TenantId, BillingCustomerId) or
/// (TenantId, QuickBooksCustomerId). The controller maps this to
/// a 409 Conflict; never collapsed to a 500.
/// </summary>
public sealed class QuickBooksCustomerMappingConflictException : InvalidOperationException
{
    public QuickBooksCustomerMappingConflictException(string message) : base(message) { }
}
