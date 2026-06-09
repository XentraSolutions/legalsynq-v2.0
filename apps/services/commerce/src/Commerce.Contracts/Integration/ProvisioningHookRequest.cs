namespace Commerce.Contracts.Integration;

/// <summary>
/// Action types a host platform can be asked to perform in response to a
/// commercial event. COM-B08 only defines the contract — Commerce never
/// invokes the host in this block.
/// </summary>
public enum ProvisioningAction
{
    Provision = 0,
    Deprovision = 1,
    Suspend = 2,
    Resume = 3,
}

/// <summary>
/// Payload for a future host-side provisioning action. Commerce hands
/// this DTO to the registered <c>IProvisioningHookPublisher</c>; the
/// no-op publisher in COM-B08 only logs and returns success.
/// </summary>
/// <param name="HostTenantRef">Required. Host the action targets.</param>
/// <param name="BillingAccountId">Commerce billing account id.</param>
/// <param name="SubscriptionId">Optional. Specific subscription, if any.</param>
/// <param name="ProductKey">Optional. Catalog product key, if applicable.</param>
/// <param name="PlanKey">Optional. Catalog plan key, if applicable.</param>
/// <param name="RequestedAction">What the host is being asked to do.</param>
/// <param name="CorrelationId">Optional correlation id for traceability.</param>
public sealed record ProvisioningHookRequest(
    HostTenantRef HostTenantRef,
    Guid BillingAccountId,
    Guid? SubscriptionId,
    string? ProductKey,
    string? PlanKey,
    ProvisioningAction RequestedAction,
    string? CorrelationId = null);

/// <summary>
/// Result returned by a provisioning hook publisher. The no-op publisher
/// always returns <see cref="Accepted"/> = true with
/// <see cref="Delivered"/> = false.
/// </summary>
public sealed record ProvisioningHookResult(
    bool Accepted,
    bool Delivered,
    string? Reason);
