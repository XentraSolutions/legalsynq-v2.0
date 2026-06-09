namespace TenantBilling.Domain.Repositories;

/// <summary>
/// Boundary for grouping multiple repository writes into a single atomic
/// database transaction. Domain services use this to ensure related writes
/// (e.g. inserting a payment + updating its parent invoice's status) succeed
/// or fail together — never half-applied.
/// </summary>
public interface IUnitOfWork
{
    /// <summary>
    /// Begin an explicit transaction scoped to the current unit of work. The
    /// returned object must be either committed via
    /// <see cref="IUnitOfWorkTransaction.CommitAsync"/> or disposed (which
    /// rolls back if uncommitted).
    /// </summary>
    Task<IUnitOfWorkTransaction> BeginTransactionAsync(CancellationToken ct = default);
}

/// <summary>
/// Active transaction handle. Subsequent repository writes against the same
/// unit of work participate in this transaction until it is committed or
/// disposed.
/// </summary>
public interface IUnitOfWorkTransaction : IAsyncDisposable
{
    /// <summary>
    /// Acquire an exclusive row lock on the invoice with the given id for the
    /// remainder of this transaction. Concurrent transactions that try to
    /// lock the same invoice will block until this one commits or is rolled
    /// back. This is how the payment flow serializes its overpayment guard
    /// against concurrent payments on the same invoice.
    /// </summary>
    Task LockInvoiceForUpdateAsync(Guid invoiceId, CancellationToken ct = default);

    /// <summary>
    /// Tenant-scoped variant of <see cref="LockInvoiceForUpdateAsync(Guid, CancellationToken)"/>:
    /// only takes the lock if the row matches both <paramref name="invoiceId"/>
    /// AND <paramref name="tenantId"/>. A caller that knows another tenant's
    /// invoice id therefore cannot acquire a lock on that tenant's row, which
    /// prevents cross-tenant lock contention even before the application-level
    /// tenant ownership check runs.
    /// </summary>
    Task LockInvoiceForUpdateAsync(Guid tenantId, Guid invoiceId, CancellationToken ct = default);

    /// <summary>
    /// Commit all writes performed within this transaction.
    /// </summary>
    Task CommitAsync(CancellationToken ct = default);
}
