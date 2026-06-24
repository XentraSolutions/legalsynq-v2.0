using Commerce.Application.Catalog.Abstractions;
using Commerce.Contracts.Catalog;
using Microsoft.AspNetCore.Mvc;

namespace Commerce.Api.Controllers.Catalog;

[ApiController]
[Route("api/commerce/catalog/prices")]
public sealed class PricesController : ControllerBase
{
    private readonly IPriceCatalogService _service;
    public PricesController(IPriceCatalogService service) => _service = service;

    [HttpPost]
    [ProducesResponseType(typeof(PriceResponse), StatusCodes.Status201Created)]
    public async Task<ActionResult<PriceResponse>> Create([FromBody] CreatePriceRequest request, CancellationToken ct)
    {
        var result = await _service.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<PriceResponse>>> List(CancellationToken ct)
        => Ok(await _service.ListAsync(ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<PriceResponse>> GetById(Guid id, CancellationToken ct)
        => Ok(await _service.GetAsync(id, ct));

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<PriceResponse>> Update(Guid id, [FromBody] UpdatePriceRequest request, CancellationToken ct)
        => Ok(await _service.UpdateAsync(id, request, ct));

    [HttpPost("{id:guid}/activate")]
    public async Task<ActionResult<PriceResponse>> Activate(Guid id, CancellationToken ct)
        => Ok(await _service.ActivateAsync(id, ct));

    [HttpPost("{id:guid}/retire")]
    public async Task<ActionResult<PriceResponse>> Retire(Guid id, CancellationToken ct)
        => Ok(await _service.RetireAsync(id, ct));
}
