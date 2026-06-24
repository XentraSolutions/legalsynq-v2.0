using TenantBilling.Domain.Entities;

namespace TenantBilling.Domain.Repositories;

/// <summary>
/// Persistence boundary for <see cref="InvoiceTemplate"/>. Methods are
/// scope-aware: every read/write expresses an explicit owner-scope
/// (Platform with no tenant id, or Tenant with a non-null tenant id)
/// so callers cannot accidentally read a Platform template from a
/// tenant context or vice-versa.
/// </summary>
public interface IInvoiceTemplateRepository
{
    Task<InvoiceTemplate> AddAsync(InvoiceTemplate template, CancellationToken ct = default);

    /// <summary>
    /// Persist mutated tracked entity state. Used by the service layer
    /// after mutating a template fetched via
    /// <see cref="GetByIdInScopeAsync"/>.
    /// </summary>
    Task UpdateAsync(InvoiceTemplate template, CancellationToken ct = default);

    /// <summary>
    /// Tenant-scoped fetch. <paramref name="tenantId"/> = null means
    /// "Platform scope" — only Platform templates are returned. A
    /// non-null tenant id only returns templates whose
    /// <c>OwnerType=Tenant</c> AND
    /// <c>BillingAccountId == tenantId</c>; tenant ownership of an
    /// arbitrary id therefore never leaks across tenants. Returns the
    /// tracked entity so the caller may mutate + UpdateAsync.
    /// </summary>
    Task<InvoiceTemplate?> GetByIdInScopeAsync(Guid? tenantId, Guid id, CancellationToken ct = default);

    /// <summary>
    /// Read-only sibling for the controller GET path. Returns null
    /// for any cross-scope id (no existence leak).
    /// </summary>
    Task<InvoiceTemplate?> GetByIdInScopeReadOnlyAsync(Guid? tenantId, Guid id, CancellationToken ct = default);

    /// <summary>
    /// List all templates in the given scope, newest first.
    /// </summary>
    Task<IReadOnlyList<InvoiceTemplate>> ListInScopeAsync(Guid? tenantId, CancellationToken ct = default);

    /// <summary>
    /// The current default template in the scope, or null when no
    /// default has been set yet. Used by the selection service and
    /// the public <c>/default</c> endpoints.
    /// </summary>
    Task<InvoiceTemplate?> GetDefaultInScopeAsync(Guid? tenantId, CancellationToken ct = default);

    /// <summary>
    /// True when the scope already has at least one default template.
    /// Drives the "auto-default the first Active template" rule in the
    /// service without a second SaveChanges round-trip.
    /// </summary>
    Task<bool> AnyDefaultInScopeAsync(Guid? tenantId, CancellationToken ct = default);

    /// <summary>
    /// Atomically clears the <c>IsDefault</c> flag on every template in
    /// the scope EXCEPT <paramref name="exceptTemplateId"/>. The
    /// service layer wraps the call (plus the SET-default on the new
    /// default template) in a single transaction so the unique-default
    /// invariant cannot transiently break. Returns the number of rows
    /// touched.
    /// </summary>
    Task<int> UnsetDefaultsInScopeAsync(
        Guid? tenantId,
        Guid exceptTemplateId,
        DateTime nowUtc,
        CancellationToken ct = default);
}
