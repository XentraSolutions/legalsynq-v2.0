using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Xenia.Application.Automation;
using Xenia.Domain.Automation;
using Xenia.Infrastructure.Persistence;

namespace Xenia.Infrastructure.Automation;

/// <summary>
/// EF Core–backed implementation of <see cref="IAutomationConfigurationService"/>.
///
/// Persists configuration entries to xn_automation_configuration.
///
/// Precedence (G7 closure):
///   Tenant scope &gt; Platform scope.
///   <see cref="GetEffectiveAsync"/> returns the tenant entry when present,
///   falling back to the platform entry, then null.
///
/// Safety:
/// - ConfigurationJson must not contain resolved secret values (enforced by callers).
/// - Tenant-scoped queries always filter on TenantId — no cross-tenant leakage.
/// - Optimistic concurrency: domain entity validates RowVersion before update.
///
/// Singleton — uses <see cref="IDbContextFactory{T}"/> per operation.
/// </summary>
internal sealed class EfAutomationConfigurationService : IAutomationConfigurationService
{
    private readonly IDbContextFactory<XeniaDbContext> _contextFactory;
    private readonly ILogger<EfAutomationConfigurationService> _logger;

    public EfAutomationConfigurationService(
        IDbContextFactory<XeniaDbContext> contextFactory,
        ILogger<EfAutomationConfigurationService> logger)
    {
        _contextFactory = contextFactory;
        _logger         = logger;
    }

    public async Task<AutomationConfigurationEntry?> GetAsync(
        string automationKey,
        string configurationNamespace,
        AutomationConfigurationScope scope,
        Guid? tenantId,
        CancellationToken ct = default)
    {
        await using var ctx = await _contextFactory.CreateDbContextAsync(ct);
        return await ctx.AutomationConfiguration
            .AsNoTracking()
            .FirstOrDefaultAsync(e =>
                e.AutomationKey == automationKey &&
                e.ConfigurationNamespace == configurationNamespace &&
                e.ScopeType == scope &&
                e.TenantId == tenantId, ct);
    }

    public async Task<IReadOnlyList<AutomationConfigurationEntry>> ListAsync(
        string automationKey,
        Guid? tenantId,
        CancellationToken ct = default)
    {
        await using var ctx = await _contextFactory.CreateDbContextAsync(ct);

        IQueryable<AutomationConfigurationEntry> query = ctx.AutomationConfiguration
            .AsNoTracking()
            .Where(e => e.AutomationKey == automationKey);

        if (tenantId.HasValue)
            query = query.Where(e =>
                e.ScopeType == AutomationConfigurationScope.Platform ||
                (e.ScopeType == AutomationConfigurationScope.Tenant && e.TenantId == tenantId));
        else
            query = query.Where(e => e.ScopeType == AutomationConfigurationScope.Platform);

        return await query.ToListAsync(ct);
    }

    public async Task<AutomationConfigurationEntry> UpsertAsync(
        AutomationConfigurationEntry entry,
        CancellationToken ct = default)
    {
        await using var ctx = await _contextFactory.CreateDbContextAsync(ct);

        var existing = await ctx.AutomationConfiguration
            .FirstOrDefaultAsync(e =>
                e.AutomationKey == entry.AutomationKey &&
                e.ConfigurationNamespace == entry.ConfigurationNamespace &&
                e.ScopeType == entry.ScopeType &&
                e.TenantId == entry.TenantId, ct);

        if (existing is null)
        {
            ctx.AutomationConfiguration.Add(entry);
        }
        else
        {
            existing.Update(
                entry.ConfigurationJson,
                entry.SchemaVersion,
                entry.SecretReferencesJson,
                entry.UpdatedBy,
                existing.RowVersion);
            ctx.AutomationConfiguration.Update(existing);
            entry = existing;
        }

        try
        {
            await ctx.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            _logger.LogWarning(ex,
                "Concurrency conflict upserting configuration for key={Key} ns={Namespace}",
                entry.AutomationKey, entry.ConfigurationNamespace);
            throw;
        }

        return entry;
    }

    public async Task<bool> DeleteAsync(
        string automationKey,
        string configurationNamespace,
        AutomationConfigurationScope scope,
        Guid? tenantId,
        CancellationToken ct = default)
    {
        await using var ctx = await _contextFactory.CreateDbContextAsync(ct);

        var existing = await ctx.AutomationConfiguration
            .FirstOrDefaultAsync(e =>
                e.AutomationKey == automationKey &&
                e.ConfigurationNamespace == configurationNamespace &&
                e.ScopeType == scope &&
                e.TenantId == tenantId, ct);

        if (existing is null) return false;

        ctx.AutomationConfiguration.Remove(existing);
        await ctx.SaveChangesAsync(ct);
        return true;
    }

    /// <inheritdoc />
    /// <remarks>
    /// Precedence: Tenant scope &gt; Platform scope.
    ///
    /// Uses a single query to fetch both candidates and selects tenant-scope first
    /// to avoid two round-trips. When tenantId is null only Platform entries are eligible.
    /// </remarks>
    public async Task<AutomationConfigurationEntry?> GetEffectiveAsync(
        string automationKey,
        string configurationNamespace,
        Guid? tenantId,
        CancellationToken ct = default)
    {
        await using var ctx = await _contextFactory.CreateDbContextAsync(ct);

        // Fetch all matching namespace entries in one query
        IQueryable<AutomationConfigurationEntry> query = ctx.AutomationConfiguration
            .AsNoTracking()
            .Where(e =>
                e.AutomationKey == automationKey &&
                e.ConfigurationNamespace == configurationNamespace);

        if (tenantId.HasValue)
        {
            // Accept Platform entries (TenantId IS NULL) or Tenant entries for this tenant
            query = query.Where(e =>
                (e.ScopeType == AutomationConfigurationScope.Platform && e.TenantId == null) ||
                (e.ScopeType == AutomationConfigurationScope.Tenant && e.TenantId == tenantId));
        }
        else
        {
            query = query.Where(e =>
                e.ScopeType == AutomationConfigurationScope.Platform && e.TenantId == null);
        }

        var candidates = await query.ToListAsync(ct);

        if (candidates.Count == 0) return null;

        // Tenant scope wins over Platform scope
        var tenantEntry = tenantId.HasValue
            ? candidates.FirstOrDefault(e => e.ScopeType == AutomationConfigurationScope.Tenant)
            : null;

        return tenantEntry
            ?? candidates.FirstOrDefault(e => e.ScopeType == AutomationConfigurationScope.Platform);
    }
}
