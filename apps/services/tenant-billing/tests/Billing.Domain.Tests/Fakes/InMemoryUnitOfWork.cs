using System.Collections.Concurrent;
using Billing.Domain.Repositories;

namespace Billing.Domain.Tests.Fakes;

/// <summary>
/// In-memory unit of work used by domain tests. Models the row-level lock
/// the relational implementation acquires by serializing on a per-invoice
/// semaphore. The semaphores are shared across all transactions created
/// from the same instance so concurrent payment attempts on the same
/// invoice block each other — exactly like SELECT ... FOR UPDATE under
/// MySQL/InnoDB.
/// </summary>
internal sealed class InMemoryUnitOfWork : IUnitOfWork
{
    private readonly ConcurrentDictionary<Guid, SemaphoreSlim> _invoiceLocks = new();

    public Task<IUnitOfWorkTransaction> BeginTransactionAsync(CancellationToken ct = default)
        => Task.FromResult<IUnitOfWorkTransaction>(new InMemoryTransaction(this));

    private SemaphoreSlim LockFor(Guid invoiceId)
        => _invoiceLocks.GetOrAdd(invoiceId, _ => new SemaphoreSlim(1, 1));

    private sealed class InMemoryTransaction : IUnitOfWorkTransaction
    {
        private readonly InMemoryUnitOfWork _owner;
        private readonly List<SemaphoreSlim> _heldLocks = new();
        private bool _disposed;

        public InMemoryTransaction(InMemoryUnitOfWork owner) => _owner = owner;

        public async Task LockInvoiceForUpdateAsync(Guid invoiceId, CancellationToken ct = default)
        {
            var sem = _owner.LockFor(invoiceId);
            await sem.WaitAsync(ct);
            _heldLocks.Add(sem);
        }

        public Task LockInvoiceForUpdateAsync(Guid tenantId, Guid invoiceId, CancellationToken ct = default)
        {
            // The fake doesn't model tenant-scoped locking semantics (the
            // domain unit tests don't exercise cross-tenant lock contention)
            // — for serialization purposes we simply lock the same per-invoice
            // semaphore. Real tenant isolation under MySQL is enforced by the
            // EfUnitOfWork SQL predicate.
            return LockInvoiceForUpdateAsync(invoiceId, ct);
        }

        public Task CommitAsync(CancellationToken ct = default) => Task.CompletedTask;

        public ValueTask DisposeAsync()
        {
            if (_disposed) return ValueTask.CompletedTask;
            _disposed = true;
            // Release all locks acquired during this transaction so the next
            // waiter (a concurrent payment attempt) can proceed.
            foreach (var sem in _heldLocks)
            {
                sem.Release();
            }
            _heldLocks.Clear();
            return ValueTask.CompletedTask;
        }
    }
}
