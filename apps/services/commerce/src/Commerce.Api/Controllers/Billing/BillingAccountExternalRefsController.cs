using Commerce.Application.Billing.Abstractions;
using Commerce.Contracts.Billing;
using Microsoft.AspNetCore.Mvc;

namespace Commerce.Api.Controllers.Billing;

[ApiController]
[Route("api/commerce/billing-accounts/{id:guid}/external-refs")]
public sealed class BillingAccountExternalRefsController : ControllerBase
{
    private readonly IBillingAccountExternalRefService _service;

    public BillingAccountExternalRefsController(IBillingAccountExternalRefService service)
        => _service = service;

    [HttpPost]
    [ProducesResponseType(typeof(ExternalRefResponse), StatusCodes.Status201Created)]
    public async Task<ActionResult<ExternalRefResponse>> Add(
        Guid id, [FromBody] CreateExternalRefRequest request, CancellationToken ct)
    {
        var result = await _service.AddAsync(id, request, ct);
        return CreatedAtAction(nameof(List), new { id }, result);
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<ExternalRefResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ExternalRefResponse>>> List(Guid id, CancellationToken ct)
        => Ok(await _service.ListAsync(id, ct));

    [HttpPut("{refId:guid}")]
    [ProducesResponseType(typeof(ExternalRefResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<ExternalRefResponse>> Update(
        Guid id, Guid refId, [FromBody] UpdateExternalRefRequest request, CancellationToken ct)
        => Ok(await _service.UpdateAsync(id, refId, request, ct));

    [HttpPost("{refId:guid}/make-primary")]
    [ProducesResponseType(typeof(ExternalRefResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<ExternalRefResponse>> MakePrimary(
        Guid id, Guid refId, CancellationToken ct)
        => Ok(await _service.MakePrimaryAsync(id, refId, ct));
}
