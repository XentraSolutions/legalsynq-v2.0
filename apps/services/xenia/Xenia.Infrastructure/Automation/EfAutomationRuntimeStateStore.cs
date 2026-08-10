using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Xenia.Domain.Automation;
using Xenia.Infrastructure.Persistence;

namespace Xenia.Infrastructure.Automation;

/// <summary>
/// EF Core–backed implementation of <see cref="IAutomationRuntimeStateStore"/>.
///
/// Replaces <see cref="InMemoryAutomationRuntimeStateStore"/> for production use.
/// Uses <see cref="IDbContextFactory{T}"/> so this singleton service can create
/// short-lived DbContext instances per operation without captive-dependency problems.
///
/// Tenant isolation:
/// - Guid.Empty sentinel = global (platform-level, no-tenant) state row.
/// - All tenant-scoped queries filter on tenant_id.
/// </summary>
internal sealed class EfAutomationRuntimeStateStore : IAutomationRuntimeStateStore
{
    private readonly IDbContextFactory<XeniaDbContext> _contextFactory;
    private readonly ILogger<EfAutomationRuntimeStateStore> _logger;

    public EfAutomationRuntimeStateStore(
        IDbContextFactory<XeniaDbContext> contextFactory,
        ILogger<EfAutomationRuntimeStateStore> logger)
    {
        _contextFactory = contextFactory;
        _logger         = logger;
    }

    public async Task<AutomationRuntimeState?> GetAsync(
        string automationKey, Guid? tenantId, CancellationToken ct = default)
    {
        var tenantKey = ToDbTenantId(tenantId);
        await using var ctx = await _contextFactory.CreateDbContextAsync(ct);
        var record = await ctx.AutomationRuntimeState
            .AsNoTracking()
            .FirstOrDefaultAsync(r =>
                r.TenantId == tenantKey &&
                r.AutomationKey == automationKey, ct);

        return record?.ToDomainModel();
    }

    public async Task UpsertAsync(AutomationRuntimeState state, CancellationToken ct = default)
    {
        var tenantKey = ToDbTenantId(state.TenantId);
        await using var ctx = await _contextFactory.CreateDbContextAsync(ct);

        var existing = await ctx.AutomationRuntimeState
            .FirstOrDefaultAsync(r =>
                r.TenantId == tenantKey &&
                r.AutomationKey == state.AutomationKey, ct);

        if (existing is null)
        {
            var record = AutomationRuntimeStateRecord.Create(
                tenantKey,
                state.AutomationKey,
                state.AutomationVersion,
                state.GlobalState);
            record.SyncFromDomainModel(state);
            ctx.AutomationRuntimeState.Add(record);
        }
        else
        {
            existing.SyncFromDomainModel(state);
            ctx.AutomationRuntimeState.Update(existing);
        }

        try
        {
            await ctx.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            _logger.LogWarning(ex,
                "Concurrency conflict upserting runtime state for key={Key} tenant={TenantId}",
                state.AutomationKey, state.TenantId);
            throw;
        }
    }

    public async Task<IReadOnlyList<AutomationRuntimeState>> ListAsync(
        Guid? tenantId, CancellationToken ct = default)
    {
        await using var ctx = await _contextFactory.CreateDbContextAsync(ct);

        IQueryable<AutomationRuntimeStateRecord> query = ctx.AutomationRuntimeState.AsNoTracking();

        if (tenantId.HasValue)
        {
            var tenantKey = ToDbTenantId(tenantId);
            query = query.Where(r => r.TenantId == tenantKey);
        }

        var records = await query.ToListAsync(ct);
        return records.ConvertAll(r => r.ToDomainModel());
    }

    private static Guid ToDbTenantId(Guid? tenantId) =>
        tenantId ?? Guid.Empty;
}
