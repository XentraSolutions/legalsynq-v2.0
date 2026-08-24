using Microsoft.EntityFrameworkCore;
using Xenia.Application.Configuration;
using Xenia.Domain.Configuration;
using Xenia.Infrastructure.Persistence;

namespace Xenia.Infrastructure.Configuration;

/// <summary>
/// EF Core-backed Xenia configuration service.
/// Resolves configuration using the precedence chain: Global → Tenant → Module → TenantModule.
/// </summary>
internal sealed class EfXeniaConfigurationService : IXeniaConfigurationService
{
    private readonly XeniaDbContext _db;

    public EfXeniaConfigurationService(XeniaDbContext db) => _db = db;

    public async Task<IReadOnlyList<ConfigurationEntryDto>> GetVisibleConfigurationAsync(
        Guid? tenantId,
        string? @namespace = null,
        CancellationToken ct = default)
    {
        var query = _db.ConfigurationEntries.AsNoTracking();

        if (@namespace is not null)
            query = query.Where(e => e.Namespace == @namespace);

        if (tenantId.HasValue)
        {
            var tenantStr = tenantId.Value.ToString();
            query = query.Where(e =>
                e.ScopeType == ScopeType.Global ||
                (e.ScopeType == ScopeType.Tenant && e.ScopeId == tenantStr) ||
                e.ScopeType == ScopeType.Module ||
                (e.ScopeType == ScopeType.TenantModule && e.ScopeId != null && e.ScopeId.StartsWith(tenantStr)));
        }

        var entries = await query.OrderBy(e => e.Namespace).ThenBy(e => e.ConfigurationKey).ToListAsync(ct);
        return entries.Select(ConfigurationEntryDto.FromEntity).ToList();
    }

    public async Task<string?> ResolveValueAsync(
        string @namespace,
        string key,
        Guid? tenantId = null,
        string? moduleKey = null,
        CancellationToken ct = default)
    {
        var candidates = await _db.ConfigurationEntries
            .AsNoTracking()
            .Where(e => e.Namespace == @namespace && e.ConfigurationKey == key && !e.IsSecret)
            .ToListAsync(ct);

        // Highest precedence wins — TenantModule → Tenant → Module → Global
        if (tenantId.HasValue && moduleKey is not null)
        {
            var scopeId = $"{tenantId.Value}:{moduleKey}";
            var tm = candidates.FirstOrDefault(e => e.ScopeType == ScopeType.TenantModule && e.ScopeId == scopeId);
            if (tm is not null) return tm.ConfigurationValue;
        }

        if (tenantId.HasValue)
        {
            var tenantStr = tenantId.Value.ToString();
            var t = candidates.FirstOrDefault(e => e.ScopeType == ScopeType.Tenant && e.ScopeId == tenantStr);
            if (t is not null) return t.ConfigurationValue;
        }

        if (moduleKey is not null)
        {
            var m = candidates.FirstOrDefault(e => e.ScopeType == ScopeType.Module && e.ScopeId == moduleKey);
            if (m is not null) return m.ConfigurationValue;
        }

        var global = candidates.FirstOrDefault(e => e.ScopeType == ScopeType.Global);
        return global?.ConfigurationValue;
    }

    public async Task SetValueAsync(
        ScopeType scopeType,
        string? scopeId,
        string @namespace,
        string key,
        string? value,
        bool isSecret = false,
        CancellationToken ct = default)
    {
        var existing = await _db.ConfigurationEntries
            .FirstOrDefaultAsync(e =>
                e.ScopeType == scopeType &&
                e.ScopeId == scopeId &&
                e.Namespace == @namespace &&
                e.ConfigurationKey == key, ct);

        if (existing is null)
        {
            var entry = new XeniaConfigurationEntry(
                Guid.CreateVersion7(), scopeType, scopeId, @namespace, key, value, isSecret: isSecret);
            _db.ConfigurationEntries.Add(entry);
        }
        else
        {
            existing.UpdateValue(value, isSecret);
        }

        await _db.SaveChangesAsync(ct);
    }
}
