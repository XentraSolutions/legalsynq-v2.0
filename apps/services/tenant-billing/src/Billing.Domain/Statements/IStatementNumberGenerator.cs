namespace Billing.Domain.Statements;

/// <summary>
/// STAT-B02 — Generates the next per-tenant statement number for a
/// given calendar year. The canonical format is
/// <c>STMT-YYYY-NNNNNN</c>; the prefix is fixed (the
/// <see cref="Entities.StatementTemplate.StatementNumberPrefix"/>
/// override does not influence the index — it is reserved for a
/// future renderer).
///
/// Concurrency: implementations are based on
/// <c>MAX(seq) + 1</c>, which is racy by construction. The
/// persistence service catches the duplicate-key error from the
/// <c>(TenantId, StatementNumber)</c> unique index and retries the
/// generation a bounded number of times. See
/// <c>STAT-B02-report.md §6.2</c> for the full rationale.
/// </summary>
public interface IStatementNumberGenerator
{
    Task<string> NextAsync(Guid tenantId, int year, CancellationToken ct = default);
}
