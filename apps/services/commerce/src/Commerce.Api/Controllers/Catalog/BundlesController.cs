using Commerce.Application.Catalog.Abstractions;
using Commerce.Contracts.Catalog;
using Microsoft.AspNetCore.Mvc;

namespace Commerce.Api.Controllers.Catalog;

[ApiController]
[Route("api/commerce/catalog/bundles")]
public sealed class BundlesController : ControllerBase
{
    private readonly IBundleCatalogService _service;
    public BundlesController(IBundleCatalogService service) => _service = service;

    [HttpPost]
    [ProducesResponseType(typeof(BundleResponse), StatusCodes.Status201Created)]
    public async Task<ActionResult<BundleResponse>> Create([FromBody] CreateBundleRequest request, CancellationToken ct)
    {
        var result = await _service.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<BundleResponse>>> List(CancellationToken ct)
        => Ok(await _service.ListAsync(ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<BundleResponse>> GetById(Guid id, CancellationToken ct)
        => Ok(await _service.GetAsync(id, ct));

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<BundleResponse>> Update(Guid id, [FromBody] UpdateBundleRequest request, CancellationToken ct)
        => Ok(await _service.UpdateAsync(id, request, ct));

    [HttpPost("{id:guid}/activate")]
    public async Task<ActionResult<BundleResponse>> Activate(Guid id, CancellationToken ct)
        => Ok(await _service.ActivateAsync(id, ct));

    [HttpPost("{id:guid}/retire")]
    public async Task<ActionResult<BundleResponse>> Retire(Guid id, CancellationToken ct)
        => Ok(await _service.RetireAsync(id, ct));

    // ---- Items ----

    [HttpPost("{bundleId:guid}/items")]
    [ProducesResponseType(typeof(BundleItemResponse), StatusCodes.Status201Created)]
    public async Task<ActionResult<BundleItemResponse>> AddItem(Guid bundleId, [FromBody] AddBundleItemRequest request, CancellationToken ct)
    {
        var result = await _service.AddItemAsync(bundleId, request, ct);
        return CreatedAtAction(nameof(ListItems), new { bundleId }, result);
    }

    [HttpGet("{bundleId:guid}/items")]
    public async Task<ActionResult<IReadOnlyList<BundleItemResponse>>> ListItems(Guid bundleId, CancellationToken ct)
        => Ok(await _service.ListItemsAsync(bundleId, ct));

    [HttpDelete("{bundleId:guid}/items/{itemId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> RemoveItem(Guid bundleId, Guid itemId, CancellationToken ct)
    {
        await _service.RemoveItemAsync(bundleId, itemId, ct);
        return NoContent();
    }
}
