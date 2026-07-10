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
    public required bool Enabled { get; init; }
    public required DateTime UpdatedAtUtc { get; init; }
}
