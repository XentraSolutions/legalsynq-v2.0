using Microsoft.EntityFrameworkCore;
using TenantBilling.Domain.Entities;
using TenantBilling.Domain.Statements;
using TenantBilling.Infrastructure.Data;

namespace TenantBilling.Infrastructure.Repositories;

/// <summary>
/// STAT-B02 — EF Core implementation of
/// <see cref="ICustomerStatementRepository"/>. Translates the
/// per-tenant statement-number unique-index violation into a typed
/// exception so the persistence service can retry with a fresh
/// number.
/// </summary>
public sealed class CustomerStatementRepository : ICustomerStatementRepository
{
    /// <summary>
    /// Name of the MySQL unique index on
    /// <c>(TenantId, StatementNumber)</c>. Matched against the
    /// duplicate-key message to surface a typed retry signal.
    /// </summary>
    private const string NumberUniqueIndexName =
        "UX_customer_statements_TenantId_StatementNumber";

    private readonly TenantBillingDbContext _db;

    public CustomerStatementRepository(TenantBillingDbContext db) => _db = db;

    public async Task<CustomerStatement> AddAsync(CustomerStatement statement, CancellationToken ct = default)
    {
        await _db.CustomerStatements.AddAsync(statement, ct);
        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (IsNumberConflict(ex))
        {
            // Detach the failed insert so the next retry can ADD a
            // new entity with a fresh number under the same context.
            _db.Entry(statement).State = EntityState.Detached;
            throw new CustomerStatementNumberConflictException(
                "Statement number collision; another writer used the next sequence value first.",
                ex);
        }
        return statement;
    }

    public async Task UpdateAsync(CustomerStatement statement, CancellationToken ct = default)
    {
        _db.CustomerStatements.Update(statement);
        await _db.SaveChangesAsync(ct);
    }

    private static bool IsNumberConflict(DbUpdateException ex)
    {
        for (Exception? cur = ex; cur is not null; cur = cur.InnerException)
        {
            if (cur.Message.Contains(NumberUniqueIndexName, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    public Task<CustomerStatement?> GetByIdInScopeAsync(Guid tenantId, Guid id, CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("TenantId is required.", nameof(tenantId));
        return _db.CustomerStatements
            .Where(s => s.TenantId == tenantId && s.Id == id)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<string?> GetLatestNumberForYearAsync(Guid tenantId, int year, CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("TenantId is required.", nameof(tenantId));
        var prefix = $"{StatementNumberGenerator.Prefix}-{year:D4}-";
        return await _db.CustomerStatements
            .AsNoTracking()
            .Where(s => s.TenantId == tenantId && s.StatementNumber.StartsWith(prefix))
            .OrderByDescending(s => s.StatementNumber)
            .Select(s => s.StatementNumber)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyList<CustomerStatement>> ListForCustomerAsync(
        Guid tenantId, Guid customerId, CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("TenantId is required.", nameof(tenantId));
        if (customerId == Guid.Empty) throw new ArgumentException("CustomerId is required.", nameof(customerId));
        return await _db.CustomerStatements
            .AsNoTracking()
            .Where(s => s.TenantId == tenantId && s.CustomerId == customerId)
            .OrderByDescending(s => s.GeneratedAtUtc)
            .ThenByDescending(s => s.StatementNumber)
            .ToListAsync(ct);
    }
}
