namespace Xenia.Application.Adapters.Interfaces;

/// <summary>
/// Platform-neutral contract for tenant service operations needed by Xenia.
/// Implemented by the infrastructure layer; the core never imports LegalSynq's Tenant service directly.
/// </summary>
public interface ITenantAdapter
{
    /// <summary>Whether this adapter is configured for the current environment.</summary>
    bool IsConfigured { get; }

    /// <summary>
    /// Validates that the tenant identified by <paramref name="tenantId"/> exists and is active.
    /// Returns false when the tenant is not found, suspended, or the adapter is unavailable.
    /// </summary>
    Task<TenantValidationResult> ValidateTenantAsync(Guid tenantId, CancellationToken ct = default);

    /// <summary>
    /// Retrieves Xenia-relevant settings from the tenant record.
    /// Returns null when the adapter is unavailable or the tenant is not found.
    /// </summary>
    Task<TenantStatusResult?> GetTenantStatusAsync(Guid tenantId, CancellationToken ct = default);
}

public sealed record TenantValidationResult(bool IsValid, bool IsAvailable, string? Message);
public sealed record TenantStatusResult(Guid TenantId, string Status, bool IsActive);
