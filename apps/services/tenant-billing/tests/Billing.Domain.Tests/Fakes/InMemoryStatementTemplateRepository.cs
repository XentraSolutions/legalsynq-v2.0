using Billing.Domain.Entities;
using Billing.Domain.StatementTemplates;

namespace Billing.Domain.Tests.Fakes;

/// <summary>
/// STAT-B02 — In-memory <see cref="IStatementTemplateRepository"/>.
/// Stores templates by id; scope check is the bare tenant equality
/// (statement templates are tenant-only). Mirrors the entity-by-
/// reference semantics of the EF tracked-entity model so service
/// mutations are observable through subsequent reads.
/// </summary>
internal sealed class InMemoryStatementTemplateRepository : IStatementTemplateRepository
{
    private readonly Dictionary<Guid, StatementTemplate> _templates = new();

    public Task<StatementTemplate> AddAsync(StatementTemplate template, CancellationToken ct = default)
    {
        _templates[template.Id] = template;
        return Task.FromResult(template);
    }

    public Task UpdateAsync(StatementTemplate template, CancellationToken ct = default)
    {
        _templates[template.Id] = template;
        return Task.CompletedTask;
    }

    public Task<StatementTemplate?> GetByIdInScopeAsync(Guid tenantId, Guid id, CancellationToken ct = default)
    {
        if (_templates.TryGetValue(id, out var t) && t.TenantId == tenantId)
            return Task.FromResult<StatementTemplate?>(t);
        return Task.FromResult<StatementTemplate?>(null);
    }

    public Task<StatementTemplate?> GetByIdInScopeReadOnlyAsync(Guid tenantId, Guid id, CancellationToken ct = default)
        => GetByIdInScopeAsync(tenantId, id, ct);

    public Task<IReadOnlyList<StatementTemplate>> ListInScopeAsync(Guid tenantId, CancellationToken ct = default)
    {
        var items = _templates.Values
            .Where(t => t.TenantId == tenantId)
            .OrderByDescending(t => t.IsDefault)
            .ThenByDescending(t => t.UpdatedAtUtc)
            .ToList();
        return Task.FromResult<IReadOnlyList<StatementTemplate>>(items);
    }

    public Task<StatementTemplate?> GetDefaultInScopeAsync(Guid tenantId, CancellationToken ct = default)
    {
        var t = _templates.Values.FirstOrDefault(x =>
            x.TenantId == tenantId && x.IsDefault && x.Status == StatementTemplateStatus.Active);
        return Task.FromResult(t);
    }

    public Task<bool> AnyDefaultInScopeAsync(Guid tenantId, CancellationToken ct = default)
        => Task.FromResult(_templates.Values.Any(t => t.TenantId == tenantId && t.IsDefault));

    public Task<int> UnsetDefaultsInScopeAsync(Guid tenantId, Guid exceptTemplateId, DateTime nowUtc, CancellationToken ct = default)
    {
        var changed = 0;
        foreach (var t in _templates.Values
            .Where(x => x.TenantId == tenantId && x.IsDefault && x.Id != exceptTemplateId))
        {
            t.IsDefault = false;
            t.UpdatedAtUtc = nowUtc;
            changed++;
        }
        return Task.FromResult(changed);
    }
}
