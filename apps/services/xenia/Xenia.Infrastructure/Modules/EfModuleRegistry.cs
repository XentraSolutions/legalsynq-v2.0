using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Xenia.Application.Modules;
using Xenia.Domain.Modules;
using Xenia.Infrastructure.Persistence;

namespace Xenia.Infrastructure.Modules;

/// <summary>
/// EF Core-backed implementation of the Xenia module registry.
/// All queries are scoped to prevent accidental cross-tenant access.
/// </summary>
internal sealed class EfModuleRegistry : IModuleRegistry, ITenantModuleRegistry
{
    private readonly XeniaDbContext _db;
    private readonly ILogger<EfModuleRegistry> _logger;

    public EfModuleRegistry(XeniaDbContext db, ILogger<EfModuleRegistry> logger)
    {
        _db = db;
        _logger = logger;
    }

    // ── IModuleRegistry ───────────────────────────────────────────────────────

    public async Task RegisterModuleAsync(
        string moduleKey,
        string name,
        string version,
        string description,
        string configurationNamespace,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleKey);

        var exists = await _db.Modules
            .AnyAsync(m => m.ModuleKey == moduleKey, ct);

        if (exists)
        {
            throw new InvalidOperationException(
                $"A module with key '{moduleKey}' is already registered. " +
                "Duplicate module registration is not permitted.");
        }

        var module = new XeniaModule(
            Guid.CreateVersion7(),
            moduleKey, name, version, description, configurationNamespace);

        _db.Modules.Add(module);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Xenia: module '{ModuleKey}' v{Version} registered successfully.",
            moduleKey, version);
    }

    public async Task<IReadOnlyList<ModuleDto>> GetModulesAsync(CancellationToken ct = default)
    {
        var modules = await _db.Modules
            .AsNoTracking()
            .OrderBy(m => m.Name)
            .ToListAsync(ct);

        return modules.Select(ModuleDto.FromEntity).ToList();
    }

    public async Task<ModuleDto?> GetModuleAsync(string moduleKey, CancellationToken ct = default)
    {
        var module = await _db.Modules
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.ModuleKey == moduleKey, ct);

        return module is null ? null : ModuleDto.FromEntity(module);
    }

    public async Task EnableModuleAsync(string moduleKey, CancellationToken ct = default)
        => await SetModuleEnabledAsync(moduleKey, enabled: true, ct);

    public async Task DisableModuleAsync(string moduleKey, CancellationToken ct = default)
        => await SetModuleEnabledAsync(moduleKey, enabled: false, ct);

    private async Task SetModuleEnabledAsync(string moduleKey, bool enabled, CancellationToken ct)
    {
        var module = await _db.Modules
            .FirstOrDefaultAsync(m => m.ModuleKey == moduleKey, ct)
            ?? throw new KeyNotFoundException($"Module '{moduleKey}' is not registered.");

        if (enabled) module.Enable(); else module.Disable();
        await _db.SaveChangesAsync(ct);
    }

    // ── ITenantModuleRegistry ─────────────────────────────────────────────────

    public async Task<IReadOnlyList<TenantModuleDto>> GetTenantModulesAsync(
        Guid tenantId, CancellationToken ct = default)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("TenantId must not be empty.", nameof(tenantId));

        var entries = await _db.TenantModules
            .AsNoTracking()
            .Where(tm => tm.TenantId == tenantId)
            .OrderBy(tm => tm.ModuleKey)
            .ToListAsync(ct);

        return entries.Select(tm => new TenantModuleDto
        {
            Id = tm.Id,
            TenantId = tm.TenantId,
            ModuleKey = tm.ModuleKey,
            Enabled = tm.Enabled,
            UpdatedAtUtc = tm.UpdatedAtUtc,
        }).ToList();
    }

    public async Task EnableModuleForTenantAsync(
        Guid tenantId, string moduleKey, CancellationToken ct = default)
        => await SetTenantModuleEnabledAsync(tenantId, moduleKey, enabled: true, ct);

    public async Task DisableModuleForTenantAsync(
        Guid tenantId, string moduleKey, CancellationToken ct = default)
        => await SetTenantModuleEnabledAsync(tenantId, moduleKey, enabled: false, ct);

    private async Task SetTenantModuleEnabledAsync(
        Guid tenantId, string moduleKey, bool enabled, CancellationToken ct)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("TenantId must not be empty.", nameof(tenantId));

        var entry = await _db.TenantModules
            .FirstOrDefaultAsync(tm => tm.TenantId == tenantId && tm.ModuleKey == moduleKey, ct);

        if (entry is null)
        {
            entry = new XeniaTenantModule(Guid.CreateVersion7(), tenantId, moduleKey);
            _db.TenantModules.Add(entry);
        }

        if (enabled) entry.Enable(); else entry.Disable();
        await _db.SaveChangesAsync(ct);
    }
}
