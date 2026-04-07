using Identity.Domain;

namespace Identity.Application.Interfaces;

public record ProvisioningResult(
    bool Success,
    string? Hostname,
    string? ErrorMessage);

public interface ITenantProvisioningService
{
    Task<ProvisioningResult> ProvisionAsync(Tenant tenant, CancellationToken ct = default);
    Task<ProvisioningResult> RetryProvisioningAsync(Tenant tenant, CancellationToken ct = default);
}
