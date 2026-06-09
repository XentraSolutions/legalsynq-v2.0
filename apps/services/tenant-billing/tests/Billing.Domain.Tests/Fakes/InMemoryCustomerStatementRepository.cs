using Billing.Domain.Entities;
using Billing.Domain.Statements;

namespace Billing.Domain.Tests.Fakes;

/// <summary>
/// STAT-B02 — In-memory <see cref="ICustomerStatementRepository"/>
/// for domain tests. Honours the per-tenant statement-number unique
/// constraint by raising <see cref="CustomerStatementNumberConflictException"/>
/// on a duplicate insert. Optionally simulates a single transient
/// collision via <see cref="SimulateNumberConflictOnce"/> so the
/// retry loop can be exercised deterministically.
/// </summary>
internal sealed class InMemoryCustomerStatementRepository : ICustomerStatementRepository
{
    private readonly Dictionary<Guid, CustomerStatement> _statements = new();

    /// <summary>
    /// When true, the next <see cref="AddAsync"/> call is rejected
    /// with a <see cref="CustomerStatementNumberConflictException"/>
    /// without persisting. The flag auto-clears after firing once.
    /// </summary>
    public bool SimulateNumberConflictOnce { get; set; }

    public IReadOnlyDictionary<Guid, CustomerStatement> All => _statements;

    public Task<CustomerStatement> AddAsync(CustomerStatement statement, CancellationToken ct = default)
    {
        if (SimulateNumberConflictOnce)
        {
            SimulateNumberConflictOnce = false;
            throw new CustomerStatementNumberConflictException("Simulated conflict.");
        }

        var clash = _statements.Values.Any(s =>
            s.TenantId == statement.TenantId && s.StatementNumber == statement.StatementNumber);
        if (clash)
        {
            throw new CustomerStatementNumberConflictException(
                $"Number '{statement.StatementNumber}' already exists for this tenant.");
        }

        _statements[statement.Id] = statement;
        return Task.FromResult(statement);
    }

    public Task UpdateAsync(CustomerStatement statement, CancellationToken ct = default)
    {
        _statements[statement.Id] = statement;
        return Task.CompletedTask;
    }

    public Task<CustomerStatement?> GetByIdInScopeAsync(Guid tenantId, Guid id, CancellationToken ct = default)
    {
        if (_statements.TryGetValue(id, out var s) && s.TenantId == tenantId)
            return Task.FromResult<CustomerStatement?>(s);
        return Task.FromResult<CustomerStatement?>(null);
    }

    public Task<string?> GetLatestNumberForYearAsync(Guid tenantId, int year, CancellationToken ct = default)
    {
        var prefix = $"{StatementNumberGenerator.Prefix}-{year:D4}-";
        var latest = _statements.Values
            .Where(s => s.TenantId == tenantId && s.StatementNumber.StartsWith(prefix))
            .OrderByDescending(s => s.StatementNumber, StringComparer.Ordinal)
            .Select(s => s.StatementNumber)
            .FirstOrDefault();
        return Task.FromResult<string?>(latest);
    }

    public Task<IReadOnlyList<CustomerStatement>> ListForCustomerAsync(
        Guid tenantId, Guid customerId, CancellationToken ct = default)
    {
        var items = _statements.Values
            .Where(s => s.TenantId == tenantId && s.CustomerId == customerId)
            .OrderByDescending(s => s.GeneratedAtUtc)
            .ThenByDescending(s => s.StatementNumber, StringComparer.Ordinal)
            .ToList();
        return Task.FromResult<IReadOnlyList<CustomerStatement>>(items);
    }
}
