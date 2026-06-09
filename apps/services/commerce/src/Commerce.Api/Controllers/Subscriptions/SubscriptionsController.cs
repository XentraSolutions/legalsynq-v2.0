using Commerce.Application.Subscriptions.Abstractions;
using Commerce.Contracts.Subscriptions;
using Microsoft.AspNetCore.Mvc;

namespace Commerce.Api.Controllers.Subscriptions;

[ApiController]
[Route("api/commerce/subscriptions")]
public sealed class SubscriptionsController : ControllerBase
{
    private readonly ISubscriptionService _service;

    public SubscriptionsController(ISubscriptionService service) => _service = service;

    [HttpPost]
    [ProducesResponseType(typeof(SubscriptionResponse), StatusCodes.Status201Created)]
    public async Task<ActionResult<SubscriptionResponse>> Create(
        [FromBody] CreateSubscriptionRequest request, CancellationToken ct)
    {
        var result = await _service.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<SubscriptionResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<SubscriptionResponse>>> List(
        [FromQuery] Guid? billingAccountId, CancellationToken ct)
        => Ok(await _service.ListAsync(billingAccountId, ct));

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(SubscriptionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SubscriptionResponse>> GetById(Guid id, CancellationToken ct)
        => Ok(await _service.GetAsync(id, ct));

    [HttpPost("{id:guid}/activate")]
    [ProducesResponseType(typeof(SubscriptionResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<SubscriptionResponse>> Activate(Guid id, CancellationToken ct)
        => Ok(await _service.ActivateAsync(id, ct));

    [HttpPost("{id:guid}/suspend")]
    [ProducesResponseType(typeof(SubscriptionResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<SubscriptionResponse>> Suspend(Guid id, CancellationToken ct)
        => Ok(await _service.SuspendAsync(id, ct));

    [HttpPost("{id:guid}/reactivate")]
    [ProducesResponseType(typeof(SubscriptionResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<SubscriptionResponse>> Reactivate(Guid id, CancellationToken ct)
        => Ok(await _service.ReactivateAsync(id, ct));

    [HttpPost("{id:guid}/cancel")]
    [ProducesResponseType(typeof(SubscriptionResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<SubscriptionResponse>> Cancel(
        Guid id, [FromBody] CancelSubscriptionRequest request, CancellationToken ct)
        => Ok(await _service.CancelAsync(id, request, ct));

    [HttpPost("{id:guid}/renew")]
    [ProducesResponseType(typeof(SubscriptionResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<SubscriptionResponse>> Renew(
        Guid id, [FromBody] RenewSubscriptionRequest? request, CancellationToken ct)
        => Ok(await _service.RenewAsync(id, request, ct));

    [HttpPost("{id:guid}/change-plan")]
    [ProducesResponseType(typeof(SubscriptionResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<SubscriptionResponse>> ChangePlan(
        Guid id, [FromBody] ChangeSubscriptionPlanRequest request, CancellationToken ct)
        => Ok(await _service.ChangePlanAsync(id, request, ct));

    [HttpGet("{id:guid}/changes")]
    [ProducesResponseType(typeof(IReadOnlyList<SubscriptionChangeResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<SubscriptionChangeResponse>>> ListChanges(
        Guid id, CancellationToken ct)
        => Ok(await _service.ListChangesAsync(id, ct));
}

[ApiController]
[Route("api/commerce/billing-accounts/{billingAccountId:guid}/subscriptions")]
public sealed class BillingAccountSubscriptionsController : ControllerBase
{
    private readonly ISubscriptionService _service;

    public BillingAccountSubscriptionsController(ISubscriptionService service) => _service = service;

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<SubscriptionResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<SubscriptionResponse>>> List(
        Guid billingAccountId, CancellationToken ct)
        => Ok(await _service.ListAsync(billingAccountId, ct));
}
