using TenantBilling.Domain.Entities;

namespace TenantBilling.Domain.StatementTemplates;

/// <summary>
/// STAT-B02 — Persistence boundary for
/// <see cref="StatementTemplate"/>. Mirrors
/// <c>IInvoiceTemplateRepository</c> but every method is
/// single-tenant scoped (no platform overload).
/// </summary>
public interface IStatementTemplateRepository
{
    Task<StatementTemplate> AddAsync(StatementTemplate template, CancellationToken ct = default);

    Task UpdateAsync(StatementTemplate template, CancellationToken ct = default);

    Task<StatementTemplate?> GetByIdInScopeAsync(Guid tenantId, Guid id, CancellationToken ct = default);

    Task<StatementTemplate?> GetByIdInScopeReadOnlyAsync(Guid tenantId, Guid id, CancellationToken ct = default);

    Task<IReadOnlyList<StatementTemplate>> ListInScopeAsync(Guid tenantId, CancellationToken ct = default);

    Task<StatementTemplate?> GetDefaultInScopeAsync(Guid tenantId, CancellationToken ct = default);

    Task<bool> AnyDefaultInScopeAsync(Guid tenantId, CancellationToken ct = default);

    Task<int> UnsetDefaultsInScopeAsync(
        Guid tenantId,
        Guid exceptTemplateId,
        DateTime nowUtc,
        CancellationToken ct = default);
}
