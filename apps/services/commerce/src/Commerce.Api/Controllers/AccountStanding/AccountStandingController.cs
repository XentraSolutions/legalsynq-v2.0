using Commerce.Application.AccountStanding.Abstractions;
using Commerce.Contracts.AccountStanding;
using Microsoft.AspNetCore.Mvc;

namespace Commerce.Api.Controllers.AccountStanding;

[ApiController]
[Route("api/commerce/billing-accounts/{billingAccountId:guid}/account-standing")]
public sealed class AccountStandingController : ControllerBase
{
    private readonly IAccountStandingService _service;
    public AccountStandingController(IAccountStandingService service) => _service = service;

    [HttpGet]
    [ProducesResponseType(typeof(AccountStandingResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AccountStandingResponse>> Get(
        Guid billingAccountId, CancellationToken ct)
        => Ok(await _service.GetAsync(billingAccountId, ct));

    [HttpPost("evaluate")]
    [ProducesResponseType(typeof(AccountStandingResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AccountStandingResponse>> Evaluate(
        Guid billingAccountId, CancellationToken ct)
        => Ok(await _service.EvaluateAsync(billingAccountId, ct));
}
