using TenantBilling.Domain.Entities;

namespace TenantBilling.Domain.Statements;

/// <summary>
/// STAT-B02 — Persistence boundary for
/// <see cref="CustomerStatement"/>. Tenant-scoped reads and writes;
/// every method takes a non-empty <paramref name="tenantId"/> so a
/// caller cannot accidentally fetch another tenant's snapshot.
/// </summary>
public interface ICustomerStatementRepository
{
    /// <summary>
    /// Insert a new persisted statement. Translates the
    /// <c>(TenantId, StatementNumber)</c> unique-index violation
    /// into a typed
    /// <see cref="CustomerStatementNumberConflictException"/> so the
    /// service layer can retry with a fresh number.
    /// </summary>
    Task<CustomerStatement> AddAsync(CustomerStatement statement, CancellationToken ct = default);

    /// <summary>Persist mutated tracked-entity state (used by void).</summary>
    Task UpdateAsync(CustomerStatement statement, CancellationToken ct = default);

    Task<CustomerStatement?> GetByIdInScopeAsync(Guid tenantId, Guid id, CancellationToken ct = default);

    /// <summary>
    /// Return the latest statement number under the
    /// <c>STMT-{year:D4}-</c> prefix for this tenant, or null when
    /// no statement has been generated for that tenant + year.
    /// Drives the <see cref="IStatementNumberGenerator"/>.
    /// </summary>
    Task<string?> GetLatestNumberForYearAsync(Guid tenantId, int year, CancellationToken ct = default);

    Task<IReadOnlyList<CustomerStatement>> ListForCustomerAsync(
        Guid tenantId, Guid customerId, CancellationToken ct = default);
}

/// <summary>
/// STAT-B02 — Raised by the repository when a persistence write
/// collides with the <c>(TenantId, StatementNumber)</c> unique
/// index. The persistence service catches this, regenerates the
/// number, and retries.
/// </summary>
public sealed class CustomerStatementNumberConflictException : InvalidOperationException
{
    public CustomerStatementNumberConflictException(string message) : base(message) { }
    public CustomerStatementNumberConflictException(string message, Exception inner) : base(message, inner) { }
}
