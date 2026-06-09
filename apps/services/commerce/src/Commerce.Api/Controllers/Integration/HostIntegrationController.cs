using Commerce.Application.Common.Time;
using Commerce.Application.Integration.Abstractions;
using Commerce.Contracts.Integration;
using Microsoft.AspNetCore.Mvc;

namespace Commerce.Api.Controllers.Integration;

/// <summary>
/// Host-neutral integration contract endpoints (COM-B08). All endpoints
/// expose Commerce-derived data only; none call host services. Mappings
/// are resolved against the local <c>BillingAccountExternalRef</c> data
/// seeded in COM-B03.
/// </summary>
[ApiController]
[Route("api/commerce/integration")]
public sealed class HostIntegrationController : ControllerBase
{
    private readonly ICommerceEntitlementSnapshotService _snapshots;
    private readonly ICommerceAccessRecommendationService _recommendations;
    private readonly IHostIdentityContextAccessor _identity;
    private readonly IHostTenantResolver _tenantResolver;
    private readonly IProvisioningHookPublisher _provisioning;
    private readonly IClock _clock;

    public HostIntegrationController(
        ICommerceEntitlementSnapshotService snapshots,
        ICommerceAccessRecommendationService recommendations,
        IHostIdentityContextAccessor identity,
        IHostTenantResolver tenantResolver,
        IProvisioningHookPublisher provisioning,
        IClock clock)
    {
        _snapshots = snapshots;
        _recommendations = recommendations;
        _identity = identity;
        _tenantResolver = tenantResolver;
        _provisioning = provisioning;
        _clock = clock;
    }

    [HttpGet("contracts/health")]
    [ProducesResponseType(typeof(IntegrationContractsHealthResponse), StatusCodes.Status200OK)]
    public ActionResult<IntegrationContractsHealthResponse> GetContractsHealth()
        => Ok(new IntegrationContractsHealthResponse(
            Status: "ok",
            IdentityContextAccessor: _identity.GetType().Name,
            TenantResolver: _tenantResolver.GetType().Name,
            ProvisioningHookPublisher: _provisioning.Name,
            GeneratedAtUtc: _clock.UtcNow));

    [HttpGet("billing-accounts/{billingAccountId:guid}/entitlement-snapshot")]
    [ProducesResponseType(typeof(CommerceEntitlementSnapshot), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CommerceEntitlementSnapshot>> GetSnapshotByBillingAccount(
        Guid billingAccountId,
        [FromQuery] bool includeAllStatuses = false,
        CancellationToken ct = default)
    {
        var snapshot = await _snapshots.GetByBillingAccountAsync(
            billingAccountId, includeAllStatuses, ct);
        if (snapshot is null)
            return NotFound(new { resource = "billing-account", id = billingAccountId });
        return Ok(snapshot);
    }

    [HttpGet("host-tenants/{hostPlatformKey}/{externalTenantId}/entitlement-snapshot")]
    [ProducesResponseType(typeof(CommerceEntitlementSnapshot), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CommerceEntitlementSnapshot>> GetSnapshotByHostTenant(
        string hostPlatformKey,
        string externalTenantId,
        [FromQuery] bool includeAllStatuses = false,
        CancellationToken ct = default)
    {
        var snapshot = await _snapshots.GetByHostTenantAsync(
            hostPlatformKey, externalTenantId, includeAllStatuses, ct);
        if (snapshot is null)
            return NotFound(new { hostPlatformKey, externalTenantId });
        return Ok(snapshot);
    }

    [HttpGet("billing-accounts/{billingAccountId:guid}/access-recommendation")]
    [ProducesResponseType(typeof(AccessRecommendationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AccessRecommendationResponse>> GetRecommendation(
        Guid billingAccountId,
        CancellationToken ct)
    {
        var recommendation = await _recommendations.GetForBillingAccountAsync(billingAccountId, ct);
        if (recommendation is null)
            return NotFound(new { resource = "billing-account", id = billingAccountId });
        return Ok(recommendation);
    }
}
