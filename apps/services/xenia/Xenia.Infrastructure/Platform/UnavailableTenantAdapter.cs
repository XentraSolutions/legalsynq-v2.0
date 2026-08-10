using Xenia.Application.Adapters.Interfaces;

namespace Xenia.Infrastructure.Platform;

/// <summary>
/// Noop implementation of <see cref="ITenantAdapter"/> for environments
/// where the platform Tenant service is not configured.
///
/// Returns honest unavailable results — never reports false success.
/// Replace with a real implementation that calls the Tenant API for production use.
/// </summary>
internal sealed class UnavailableTenantAdapter : ITenantAdapter
{
    private const string UnconfiguredMessage =
        "Tenant adapter is not configured. Wire a real ITenantAdapter to enable tenant validation.";

    public bool IsConfigured => false;

    public Task<TenantValidationResult> ValidateTenantAsync(Guid tenantId, CancellationToken ct = default)
        => Task.FromResult(new TenantValidationResult(
            IsValid: false,
            IsAvailable: false,
            Message: UnconfiguredMessage));

    public Task<TenantStatusResult?> GetTenantStatusAsync(Guid tenantId, CancellationToken ct = default)
        => Task.FromResult<TenantStatusResult?>(null);
}
