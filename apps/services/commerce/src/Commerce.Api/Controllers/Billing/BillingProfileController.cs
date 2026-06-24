using Commerce.Application.Billing.Abstractions;
using Commerce.Contracts.Billing;
using Microsoft.AspNetCore.Mvc;

namespace Commerce.Api.Controllers.Billing;

[ApiController]
[Route("api/commerce/billing-accounts/{id:guid}/profile")]
public sealed class BillingProfileController : ControllerBase
{
    private readonly IBillingProfileService _service;

    public BillingProfileController(IBillingProfileService service) => _service = service;

    [HttpGet]
    [ProducesResponseType(typeof(BillingProfileResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<BillingProfileResponse>> Get(Guid id, CancellationToken ct)
        => Ok(await _service.GetAsync(id, ct));

    [HttpPut]
    [ProducesResponseType(typeof(BillingProfileResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<BillingProfileResponse>> Update(
        Guid id, [FromBody] UpdateBillingProfileRequest request, CancellationToken ct)
        => Ok(await _service.UpdateAsync(id, request, ct));
}
