using Commerce.Application.Integration.Abstractions;
using Commerce.Contracts.Integration;
using Microsoft.Extensions.Logging;

namespace Commerce.Infrastructure.Integration.HostAdapters;

/// <summary>
/// COM-B08 default publisher. Does not contact any host. Logs the
/// requested action and returns an "accepted but not delivered" result
/// so callers can be wired correctly without any host being present.
///
/// A real publisher (future integration phase) would call the host's
/// provisioning API.
/// </summary>
internal sealed class NoopProvisioningHookPublisher : IProvisioningHookPublisher
{
    private readonly ILogger<NoopProvisioningHookPublisher> _logger;

    public NoopProvisioningHookPublisher(ILogger<NoopProvisioningHookPublisher> logger)
        => _logger = logger;

    public string Name => "noop";

    public Task<ProvisioningHookResult> PublishAsync(
        ProvisioningHookRequest request,
        CancellationToken ct)
    {
        _logger.LogInformation(
            "ProvisioningHook (noop) accepted action {Action} for billing account {BillingAccountId} "
            + "(host={HostPlatformKey}, tenant={ExternalTenantId}, product={ProductKey}, plan={PlanKey}, "
            + "subscription={SubscriptionId}, correlationId={CorrelationId})",
            request.RequestedAction,
            request.BillingAccountId,
            request.HostTenantRef.HostPlatformKey,
            request.HostTenantRef.ExternalTenantId,
            request.ProductKey,
            request.PlanKey,
            request.SubscriptionId,
            request.CorrelationId);

        return Task.FromResult(new ProvisioningHookResult(
            Accepted: true,
            Delivered: false,
            Reason: "noop publisher: no host adapter registered (COM-B08)."));
    }
}
