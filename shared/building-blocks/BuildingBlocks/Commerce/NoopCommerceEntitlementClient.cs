namespace BuildingBlocks.Commerce;

/// <summary>
/// Noop implementation of <see cref="ICommerceEntitlementClient"/> that
/// returns <see cref="CommerceEntitlementResult.Unavailable"/> for every call.
///
/// <para>
/// Registered automatically when <c>CommerceIntegration:Enabled = false</c>
/// (the default). Services operate in permissive standalone mode — no Commerce
/// HTTP calls are made.
/// </para>
/// </summary>
internal sealed class NoopCommerceEntitlementClient : ICommerceEntitlementClient
{
    private static readonly CommerceEntitlementResult _unavailable =
        CommerceEntitlementResult.Unavailable("Commerce integration is disabled (noop).");

    public Task<CommerceEntitlementResult> GetByHostTenantAsync(
        string            hostPlatformKey,
        string            externalTenantId,
        CancellationToken ct = default)
        => Task.FromResult(_unavailable);

    public Task<CommerceEntitlementResult> GetByBillingAccountAsync(
        Guid              billingAccountId,
        CancellationToken ct = default)
        => Task.FromResult(_unavailable);
}
