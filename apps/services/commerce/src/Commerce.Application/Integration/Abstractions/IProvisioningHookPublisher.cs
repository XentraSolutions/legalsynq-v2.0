using Commerce.Contracts.Integration;

namespace Commerce.Application.Integration.Abstractions;

/// <summary>
/// Sends provisioning hook requests to the registered host adapter.
/// COM-B08 ships with a no-op publisher only — no host integration is
/// performed in this block.
/// </summary>
public interface IProvisioningHookPublisher
{
    /// <summary>
    /// Stable identifier for the publisher implementation (used by the
    /// integration health endpoint).
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Publish a hook request. The no-op publisher accepts the request
    /// and returns immediately without contacting any host.
    /// </summary>
    Task<ProvisioningHookResult> PublishAsync(
        ProvisioningHookRequest request,
        CancellationToken ct);
}
