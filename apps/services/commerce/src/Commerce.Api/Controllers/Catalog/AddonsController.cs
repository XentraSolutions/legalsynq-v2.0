using Commerce.Application.Catalog.Abstractions;
using Commerce.Contracts.Catalog;
using Microsoft.AspNetCore.Mvc;

namespace Commerce.Api.Controllers.Catalog;

[ApiController]
[Route("api/commerce/catalog/addons")]
public sealed class AddonsController : ControllerBase
{
    private readonly IAddonCatalogService _service;
    public AddonsController(IAddonCatalogService service) => _service = service;

    [HttpPost]
    [ProducesResponseType(typeof(AddonResponse), StatusCodes.Status201Created)]
    public async Task<ActionResult<AddonResponse>> Create([FromBody] CreateAddonRequest request, CancellationToken ct)
    {
        var result = await _service.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AddonResponse>>> List(CancellationToken ct)
        => Ok(await _service.ListAsync(ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<AddonResponse>> GetById(Guid id, CancellationToken ct)
        => Ok(await _service.GetAsync(id, ct));

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<AddonResponse>> Update(Guid id, [FromBody] UpdateAddonRequest request, CancellationToken ct)
        => Ok(await _service.UpdateAsync(id, request, ct));

    [HttpPost("{id:guid}/activate")]
    public async Task<ActionResult<AddonResponse>> Activate(Guid id, CancellationToken ct)
        => Ok(await _service.ActivateAsync(id, ct));

    [HttpPost("{id:guid}/retire")]
    public async Task<ActionResult<AddonResponse>> Retire(Guid id, CancellationToken ct)
        => Ok(await _service.RetireAsync(id, ct));
}
