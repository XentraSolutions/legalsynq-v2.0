using Xenia.Domain.Configuration;

namespace Xenia.Application.Configuration;

/// <summary>
/// Read model for a Xenia configuration entry.
/// Secret values are masked — this is safe to return from APIs.
/// </summary>
public sealed record ConfigurationEntryDto
{
    public required Guid Id { get; init; }
    public required string ScopeType { get; init; }
    public required string? ScopeId { get; init; }
    public required string Namespace { get; init; }
    public required string ConfigurationKey { get; init; }

    /// <summary>
    /// Configuration value. Null when <see cref="IsSecret"/> is true
    /// (secrets are never returned via the API).
    /// </summary>
    public required string? ConfigurationValue { get; init; }

    public required string? ValueType { get; init; }
    public required bool IsSecret { get; init; }
    public required int Version { get; init; }
    public required DateTime UpdatedAtUtc { get; init; }

    public static ConfigurationEntryDto FromEntity(XeniaConfigurationEntry e) => new()
    {
        Id = e.Id,
        ScopeType = e.ScopeType.ToString(),
        ScopeId = e.ScopeId,
        Namespace = e.Namespace,
        ConfigurationKey = e.ConfigurationKey,
        ConfigurationValue = e.IsSecret ? null : e.ConfigurationValue,
        ValueType = e.IsSecret ? null : e.ValueType,
        IsSecret = e.IsSecret,
        Version = e.Version,
        UpdatedAtUtc = e.UpdatedAtUtc,
    };
}
