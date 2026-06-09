using Commerce.Application.Billing.Abstractions;
using Commerce.Contracts.Billing;
using Microsoft.AspNetCore.Mvc;

namespace Commerce.Api.Controllers.Billing;

[ApiController]
[Route("api/commerce/billing-accounts/{id:guid}/audit-events")]
public sealed class BillingAccountAuditController : ControllerBase
{
    private readonly IBillingAccountAuditService _service;

    public BillingAccountAuditController(IBillingAccountAuditService service) => _service = service;

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<BillingAccountAuditEventResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<BillingAccountAuditEventResponse>>> List(
        Guid id, CancellationToken ct)
        => Ok(await _service.ListAsync(id, ct));
}
