namespace Billing.Domain.Accounting.Erp.QuickBooks;

/// <summary>
/// MS-BILL-ERP-003 — Tenant-admin CRUD orchestration for
/// <see cref="QuickBooksCustomerMapping"/>, plus the read-only
/// resolver consumed by the QB provider.
///
/// <para>
/// All write methods accept the operator's display name (sourced
/// from the BFF-injected <c>X-User-DisplayName</c> header on the
/// controller side; never browser-trusted for authorization) and
/// stamp <c>CreatedAtUtc</c> / <c>UpdatedAtUtc</c> server-side via
/// <c>TimeProvider</c>. Read methods filter by tenant.
/// </para>
/// </summary>
public interface IQuickBooksCustomerMappingService
{
    Task<QuickBooksCustomerMapping> CreateAsync(
        Guid tenantId,
        CreateQuickBooksCustomerMappingCommand command,
        string actor,
        CancellationToken ct = default);

    Task<QuickBooksCustomerMapping> UpdateAsync(
        Guid tenantId,
        Guid id,
        UpdateQuickBooksCustomerMappingCommand command,
        CancellationToken ct = default);

    Task<bool> DeleteAsync(
        Guid tenantId,
        Guid id,
        CancellationToken ct = default);

    Task<QuickBooksCustomerMapping?> GetAsync(
        Guid tenantId,
        Guid id,
        CancellationToken ct = default);

    Task<IReadOnlyList<QuickBooksCustomerMapping>> ListAsync(
        Guid tenantId,
        int page,
        int pageSize,
        CancellationToken ct = default);

    /// <summary>
    /// Resolver entry point used by the QB provider. Returns the
    /// active mapping for the Billing customer, or NULL when:
    /// no row exists, or the row is <c>Disabled</c>. Callers MUST
    /// treat NULL as "consult the configured fallback, then fail
    /// deterministically" — they MUST NOT fuzzy-match on
    /// <see cref="QuickBooksCustomerMapping.QuickBooksDisplayName"/>
    /// or auto-create a QBO customer.
    /// </summary>
    Task<QuickBooksCustomerMapping?> ResolveActiveByBillingCustomerAsync(
        Guid tenantId,
        Guid billingCustomerId,
        CancellationToken ct = default);

    /// <summary>
    /// Best-effort audit stamp invoked by the QB provider after a
    /// successful post that resolved through this mapping. Caller
    /// MUST swallow exceptions — failure to stamp does not roll
    /// back the export.
    /// </summary>
    Task TouchLastExportedAsync(
        Guid tenantId,
        Guid id,
        CancellationToken ct = default);
}

/// <summary>Create-time payload (validated by the service).</summary>
public sealed record CreateQuickBooksCustomerMappingCommand(
    Guid BillingCustomerId,
    string QuickBooksCustomerId,
    string? QuickBooksDisplayName,
    string MappingStatus,
    string? ExportMode);

/// <summary>Update-time payload (validated by the service).</summary>
public sealed record UpdateQuickBooksCustomerMappingCommand(
    string QuickBooksCustomerId,
    string? QuickBooksDisplayName,
    string MappingStatus,
    string? ExportMode);
