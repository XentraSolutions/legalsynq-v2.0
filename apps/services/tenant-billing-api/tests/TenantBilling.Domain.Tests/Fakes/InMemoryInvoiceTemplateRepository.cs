using TenantBilling.Domain.Entities;
using TenantBilling.Domain.Repositories;

namespace TenantBilling.Domain.Tests.Fakes;

/// <summary>
/// In-memory <see cref="IInvoiceTemplateRepository"/> for domain unit
/// tests. Mirrors the EF implementation's scope rules:
/// platform = (OwnerType=Platform, BillingAccountId=null);
/// tenant   = (OwnerType=Tenant,   BillingAccountId=tenantId).
/// We intentionally store templates with their original references —
/// not deep clones — so tests can mutate after-the-fact and observe
/// the result through subsequent reads, the same way EF's tracked
/// entities would behave.
/// </summary>
internal sealed class InMemoryInvoiceTemplateRepository : IInvoiceTemplateRepository
{
    private readonly Dictionary<Guid, InvoiceTemplate> _templates = new();

    public Task<InvoiceTemplate> AddAsync(InvoiceTemplate template, CancellationToken ct = default)
    {
        _templates[template.Id] = template;
        return Task.FromResult(template);
    }

    public Task UpdateAsync(InvoiceTemplate template, CancellationToken ct = default)
    {
        _templates[template.Id] = template;
        return Task.CompletedTask;
    }

    public Task<InvoiceTemplate?> GetByIdInScopeAsync(Guid? tenantId, Guid id, CancellationToken ct = default)
    {
        if (_templates.TryGetValue(id, out var t) && InScope(t, tenantId))
            return Task.FromResult<InvoiceTemplate?>(t);
        return Task.FromResult<InvoiceTemplate?>(null);
    }

    public Task<InvoiceTemplate?> GetByIdInScopeReadOnlyAsync(Guid? tenantId, Guid id, CancellationToken ct = default)
        => GetByIdInScopeAsync(tenantId, id, ct);

    public Task<IReadOnlyList<InvoiceTemplate>> ListInScopeAsync(Guid? tenantId, CancellationToken ct = default)
    {
        var items = _templates.Values
            .Where(t => InScope(t, tenantId))
            .OrderByDescending(t => t.IsDefault)
            .ThenByDescending(t => t.UpdatedAtUtc)
            .ToList();
        return Task.FromResult<IReadOnlyList<InvoiceTemplate>>(items);
    }

    public Task<InvoiceTemplate?> GetDefaultInScopeAsync(Guid? tenantId, CancellationToken ct = default)
    {
        var t = _templates.Values.FirstOrDefault(x =>
            InScope(x, tenantId) && x.IsDefault && x.Status == InvoiceTemplateStatus.Active);
        return Task.FromResult(t);
    }

    public Task<bool> AnyDefaultInScopeAsync(Guid? tenantId, CancellationToken ct = default)
        => Task.FromResult(_templates.Values.Any(t => InScope(t, tenantId) && t.IsDefault));

    public Task<int> UnsetDefaultsInScopeAsync(Guid? tenantId, Guid exceptTemplateId, DateTime nowUtc, CancellationToken ct = default)
    {
        var changed = 0;
        foreach (var t in _templates.Values
            .Where(x => InScope(x, tenantId) && x.IsDefault && x.Id != exceptTemplateId))
        {
            t.IsDefault = false;
            t.UpdatedAtUtc = nowUtc;
            changed++;
        }
        return Task.FromResult(changed);
    }

    private static bool InScope(InvoiceTemplate t, Guid? tenantId) =>
        tenantId is null
            ? t.OwnerType == InvoiceTemplateOwnerType.Platform && t.BillingAccountId is null
            : t.OwnerType == InvoiceTemplateOwnerType.Tenant && t.BillingAccountId == tenantId;
}
