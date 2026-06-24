using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using TenantBilling.Domain.Repositories;

namespace TenantBilling.Infrastructure.Data;

/// <summary>
/// EF Core-backed unit of work. Wraps the scoped <see cref="TenantBillingDbContext"/>
/// in an explicit DB transaction so repository writes that share the same
/// scope commit or roll back atomically.
///
/// On the InMemory provider (used as a fallback when no MySQL connection is
/// configured) explicit transactions and row-level locks are not available, so
/// the unit of work degrades to a no-op transaction. That keeps the host
/// startable without MySQL but does not provide real atomicity — production
/// must always run against the relational provider.
/// </summary>
public sealed class EfUnitOfWork : IUnitOfWork
{
    private readonly TenantBillingDbContext _db;

    public EfUnitOfWork(TenantBillingDbContext db) => _db = db;

    public async Task<IUnitOfWorkTransaction> BeginTransactionAsync(CancellationToken ct = default)
    {
        if (_db.Database.IsRelational())
        {
            var tx = await _db.Database.BeginTransactionAsync(ct);
            return new EfRelationalTransaction(_db, tx);
        }

        return new EfNoopTransaction();
    }

    private sealed class EfRelationalTransaction : IUnitOfWorkTransaction
    {
        private readonly TenantBillingDbContext _db;
        private readonly IDbContextTransaction _tx;
        private bool _disposed;

        public EfRelationalTransaction(TenantBillingDbContext db, IDbContextTransaction tx)
        {
            _db = db;
            _tx = tx;
        }

        public async Task LockInvoiceForUpdateAsync(Guid invoiceId, CancellationToken ct = default)
        {
            // SELECT ... FOR UPDATE is a relational lock acquired inside the
            // active transaction. Concurrent transactions trying to lock the
            // same invoice row will block until this one commits or rolls
            // back, serializing the overpayment guard. Materialize via
            // FromSqlRaw + ToListAsync so the parameter is sent through EF's
            // value converter pipeline (Pomelo stores Guid as char(36)).
            _ = await _db.Invoices
                .FromSqlRaw("SELECT * FROM invoices WHERE Id = {0} FOR UPDATE", invoiceId)
                .AsNoTracking()
                .ToListAsync(ct);
        }

        public async Task LockInvoiceForUpdateAsync(Guid tenantId, Guid invoiceId, CancellationToken ct = default)
        {
            // Tenant-scoped lock: the predicate includes TenantId, so a caller
            // that knows a foreign tenant's invoice id cannot use this method
            // to acquire a lock on their row (the WHERE clause matches zero
            // rows and FOR UPDATE locks nothing). Combined with the
            // tenant-scoped invoice fetch that follows in the service layer,
            // this gives belt-and-braces tenant isolation across the payment
            // recording transaction.
            _ = await _db.Invoices
                .FromSqlRaw("SELECT * FROM invoices WHERE Id = {0} AND TenantId = {1} FOR UPDATE", invoiceId, tenantId)
                .AsNoTracking()
                .ToListAsync(ct);
        }

        public Task CommitAsync(CancellationToken ct = default) => _tx.CommitAsync(ct);

        public async ValueTask DisposeAsync()
        {
            if (_disposed) return;
            _disposed = true;
            await _tx.DisposeAsync();
        }
    }

    private sealed class EfNoopTransaction : IUnitOfWorkTransaction
    {
        public Task LockInvoiceForUpdateAsync(Guid invoiceId, CancellationToken ct = default)
            => Task.CompletedTask;

        public Task LockInvoiceForUpdateAsync(Guid tenantId, Guid invoiceId, CancellationToken ct = default)
            => Task.CompletedTask;

        public Task CommitAsync(CancellationToken ct = default) => Task.CompletedTask;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
