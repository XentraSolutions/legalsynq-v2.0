using Xenia.Domain.Configuration;

namespace Xenia.Application.Configuration;

/// <summary>
/// Provides layered configuration resolution for Xenia.
///
/// Configuration is resolved in ascending precedence order:
///   Global → Tenant → Module → TenantModule
///
/// Secrets are never returned through public-facing methods.
/// </summary>
public interface IXeniaConfigurationService
{
    /// <summary>
    /// Returns non-secret configuration entries visible to the current caller.
    /// Applies scope and authorization restrictions.
    /// </summary>
    Task<IReadOnlyList<ConfigurationEntryDto>> GetVisibleConfigurationAsync(
        Guid? tenantId,
        string? @namespace = null,
        CancellationToken ct = default);

    /// <summary>
    /// Resolves the effective value for a specific key using the precedence chain.
    /// Returns null when no value is configured at any scope.
    /// Never returns secret values.
    /// </summary>
    Task<string?> ResolveValueAsync(
        string @namespace,
        string key,
        Guid? tenantId = null,
        string? moduleKey = null,
        CancellationToken ct = default);

    /// <summary>
    /// Sets or updates a configuration entry.
    /// Throws when attempting to set a secret value as plaintext (use secret references).
    /// </summary>
    Task SetValueAsync(
        ScopeType scopeType,
        string? scopeId,
        string @namespace,
        string key,
        string? value,
        bool isSecret = false,
        CancellationToken ct = default);
}
