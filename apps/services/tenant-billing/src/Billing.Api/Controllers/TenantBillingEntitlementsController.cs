using Microsoft.AspNetCore.Mvc;
using Billing.Api.Contracts;
using Billing.Api.Tenancy;
using Billing.Domain.Services;

namespace Billing.Api.Controllers;

/// <summary>
/// TB-DATA-02 — REST surface over <see cref="ITenantBillingEntitlementService"/>
/// and <see cref="ITenantBillingEnablementResolver"/>. Tenant-scoped via
/// <see cref="ITenantContext"/>; gated by <c>RequireInternalTokenMiddleware</c>.
/// Internal/admin only — no customer-facing surface.
/// </summary>
[ApiController]
[Route("api/tenant-billing/entitlements")]
public sealed class TenantBillingEntitlementsController : ControllerBase
{
    private readonly ITenantBillingEntitlementService _service;
    private readonly ITenantBillingEnablementResolver _enablement;
    private readonly ITenantContext _tenant;

    public TenantBillingEntitlementsController(
        ITenantBillingEntitlementService service,
        ITenantBillingEnablementResolver enablement,
        ITenantContext tenant)
    {
        _service    = service;
        _enablement = enablement;
        _tenant     = tenant;
    }

    [HttpPost("apply")]
    [ProducesResponseType(typeof(TenantBillingEntitlementSnapshotResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Apply(
        [FromBody] ApplyEntitlementSnapshotRequestDto request,
        CancellationToken ct)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        try
        {
            var snap = await _service.ApplySnapshotAsync(_tenant.TenantId, request.ToDomain(), ct);
            return Ok(TenantBillingEntitlementSnapshotResponse.From(snap));
        }
        catch (TenantBillingProfileNotFoundException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status404NotFound);
        }
        catch (TenantBillingEntitlementClosedProfileException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status409Conflict);
        }
        catch (TenantBillingEntitlementProfileMismatchException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status409Conflict);
        }
        catch (TenantBillingEntitlementInvalidJsonException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }
        catch (ArgumentException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }
    }

    [HttpGet("current")]
    [ProducesResponseType(typeof(TenantBillingEntitlementSnapshotResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetCurrent(CancellationToken ct)
    {
        var snap = await _service.GetCurrentSnapshotAsync(_tenant.TenantId, ct);
        return snap is null
            ? Problem(detail: "No entitlement snapshot for the tenant's active profile.",
                      statusCode: StatusCodes.Status404NotFound)
            : Ok(TenantBillingEntitlementSnapshotResponse.From(snap));
    }

    [HttpGet("access")]
    [ProducesResponseType(typeof(TenantBillingAccessDecisionResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAccess(CancellationToken ct)
    {
        var decision = await _enablement.GetTenantBillingAccessAsync(_tenant.TenantId, ct);
        return Ok(TenantBillingAccessDecisionResponse.From(decision));
    }
}

/// <summary>
/// Profile-scoped read of the entitlement snapshot. Lives on its own
/// route under /api/tenant-billing/profiles to match the brief
/// (<c>GET /api/tenant-billing/profiles/{profileId}/entitlement</c>).
/// </summary>
[ApiController]
[Route("api/tenant-billing/profiles/{profileId:guid}/entitlement")]
public sealed class TenantBillingProfileEntitlementController : ControllerBase
{
    private readonly ITenantBillingEntitlementService _service;
    private readonly ITenantContext _tenant;

    public TenantBillingProfileEntitlementController(
        ITenantBillingEntitlementService service, ITenantContext tenant)
    {
        _service = service;
        _tenant  = tenant;
    }

    [HttpGet]
    [ProducesResponseType(typeof(TenantBillingEntitlementSnapshotResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(Guid profileId, CancellationToken ct)
    {
        if (profileId == Guid.Empty)
            return Problem(detail: "profileId is required.", statusCode: StatusCodes.Status400BadRequest);

        var snap = await _service.GetByProfileIdAsync(_tenant.TenantId, profileId, ct);
        return snap is null
            ? Problem(detail: $"No entitlement snapshot for profile {profileId}.",
                      statusCode: StatusCodes.Status404NotFound)
            : Ok(TenantBillingEntitlementSnapshotResponse.From(snap));
    }
}
