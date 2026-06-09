namespace BuildingBlocks.Commerce;

/// <summary>
/// Platform-wide Commerce entitlement resolution contract.
///
/// <para>
/// Consuming services (Liens, Fund, CareConnect, etc.) depend on this
/// interface — never on Commerce's internal service or DbContext — to
/// resolve the commercial access posture of a tenant.
/// </para>
///
/// <para>
/// Two implementations are provided by <c>BuildingBlocks</c>:
/// <list type="bullet">
///   <item><description>
///     <c>HttpCommerceEntitlementClient</c> — calls Commerce's
///     <c>GET /api/commerce/integration/host-tenants/{key}/{id}/entitlement-snapshot</c>
///     endpoint when <c>CommerceIntegration:Enabled = true</c>.
///   </description></item>
///   <item><description>
///     <c>NoopCommerceEntitlementClient</c> — returns
///     <see cref="CommerceEntitlementResult.Unavailable"/> for all calls
///     (safe standalone / not-yet-integrated mode).
///   </description></item>
/// </list>
/// </para>
///
/// <para>
/// <b>Never block business operations on Commerce availability.</b>
/// When <see cref="CommerceEntitlementResult.IsAvailable"/> is <c>false</c>
/// the caller should apply a local permissive fallback, not reject the request.
/// </para>
/// </summary>
public interface ICommerceEntitlementClient
{
    /// <summary>
    /// Resolve entitlement by host-platform tenant reference.
    /// </summary>
    /// <param name="hostPlatformKey">
    /// Stable host platform key registered in Commerce (e.g. <c>"legalsynq"</c>).
    /// </param>
    /// <param name="externalTenantId">
    /// The host platform's tenant identifier (typically a GUID string).
    /// </param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// A <see cref="CommerceEntitlementResult"/> describing the tenant's
    /// commercial access posture; never throws.
    /// </returns>
    Task<CommerceEntitlementResult> GetByHostTenantAsync(
        string            hostPlatformKey,
        string            externalTenantId,
        CancellationToken ct = default);

    /// <summary>
    /// Resolve entitlement by Commerce billing account id.
    /// </summary>
    /// <param name="billingAccountId">Commerce billing account primary key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// A <see cref="CommerceEntitlementResult"/>; never throws.
    /// </returns>
    Task<CommerceEntitlementResult> GetByBillingAccountAsync(
        Guid              billingAccountId,
        CancellationToken ct = default);
}
