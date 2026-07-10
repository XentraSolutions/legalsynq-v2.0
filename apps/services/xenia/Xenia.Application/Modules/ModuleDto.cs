using Xenia.Domain.Modules;

namespace Xenia.Application.Modules;

/// <summary>
/// Read model for a registered Xenia module. Safe to return from APIs.
/// </summary>
public sealed record ModuleDto
{
    public required Guid Id { get; init; }
    public required string ModuleKey { get; init; }
    public required string Name { get; init; }
    public required string Version { get; init; }
    public required string Description { get; init; }
    public required bool GlobalEnabled { get; init; }
    public required string Status { get; init; }
    public required string ConfigurationNamespace { get; init; }
    public required DateTime CreatedAtUtc { get; init; }
    public required DateTime UpdatedAtUtc { get; init; }

    public static ModuleDto FromEntity(XeniaModule m) => new()
    {
        Id = m.Id,
        ModuleKey = m.ModuleKey,
        Name = m.Name,
        Version = m.Version,
        Description = m.Description,
        GlobalEnabled = m.GlobalEnabled,
        Status = m.Status.ToString(),
        ConfigurationNamespace = m.ConfigurationNamespace,
        CreatedAtUtc = m.CreatedAtUtc,
        UpdatedAtUtc = m.UpdatedAtUtc,
    };
}

/// <summary>
/// Per-tenant module state returned by the registry.
/// </summary>
public sealed record TenantModuleDto
{
    public required Guid Id { get; init; }
    public required Guid TenantId { get; init; }
    public required string ModuleKey { get; init; }
    /// <summary>Whether the tenant has enabled this module.</summary>
    public required bool Enabled { get; init; }
    public required DateTime UpdatedAtUtc { get; init; }
}

/// <summary>
/// Combined view of global + per-tenant module state.
/// EffectiveEnabled = GlobalEnabled AND TenantEnabled.
/// A module that is globally disabled cannot be activated by tenants.
/// </summary>
public sealed record EffectiveModuleDto
{
    public required string ModuleKey { get; init; }
    public required string Name { get; init; }
    public required string Version { get; init; }
    public required string Description { get; init; }

    /// <summary>Platform-level global switch. Controlled by platform admins.</summary>
    public required bool GlobalEnabled { get; init; }

    /// <summary>Tenant-level switch. Controlled by tenant admins.</summary>
    public required bool TenantEnabled { get; init; }

    /// <summary>
    /// Effective enablement = GlobalEnabled AND TenantEnabled.
    /// This is the value modules should use to gate features.
    /// </summary>
    public bool EffectiveEnabled => GlobalEnabled && TenantEnabled;

    public required string Status { get; init; }
    public required string ConfigurationNamespace { get; init; }

    public static EffectiveModuleDto From(ModuleDto global, TenantModuleDto? tenant) => new()
    {
        ModuleKey = global.ModuleKey,
        Name = global.Name,
        Version = global.Version,
        Description = global.Description,
        GlobalEnabled = global.GlobalEnabled,
        TenantEnabled = tenant?.Enabled ?? false,
        Status = global.Status,
        ConfigurationNamespace = global.ConfigurationNamespace,
    };
}
