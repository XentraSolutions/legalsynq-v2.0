using Commerce.Application.Catalog.Abstractions;
using Commerce.Contracts.Catalog;
using Microsoft.AspNetCore.Mvc;

namespace Commerce.Api.Controllers.Catalog;

[ApiController]
[Route("api/commerce/catalog")]
public sealed class FeaturesController : ControllerBase
{
    private readonly IFeatureCatalogService _service;
    public FeaturesController(IFeatureCatalogService service) => _service = service;

    [HttpPost("products/{productId:guid}/features")]
    [ProducesResponseType(typeof(FeatureResponse), StatusCodes.Status201Created)]
    public async Task<ActionResult<FeatureResponse>> Create(Guid productId, [FromBody] CreateFeatureRequest request, CancellationToken ct)
    {
        var result = await _service.CreateAsync(productId, request, ct);
        return CreatedAtAction(nameof(ListByProduct), new { productId }, result);
    }

    [HttpGet("products/{productId:guid}/features")]
    [ProducesResponseType(typeof(IReadOnlyList<FeatureResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<FeatureResponse>>> ListByProduct(Guid productId, CancellationToken ct)
        => Ok(await _service.ListByProductAsync(productId, ct));

    [HttpPut("features/{id:guid}")]
    [ProducesResponseType(typeof(FeatureResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<FeatureResponse>> Update(Guid id, [FromBody] UpdateFeatureRequest request, CancellationToken ct)
        => Ok(await _service.UpdateAsync(id, request, ct));

    [HttpPost("features/{id:guid}/activate")]
    [ProducesResponseType(typeof(FeatureResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<FeatureResponse>> Activate(Guid id, CancellationToken ct)
        => Ok(await _service.ActivateAsync(id, ct));

    [HttpPost("features/{id:guid}/retire")]
    [ProducesResponseType(typeof(FeatureResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<FeatureResponse>> Retire(Guid id, CancellationToken ct)
        => Ok(await _service.RetireAsync(id, ct));
}
