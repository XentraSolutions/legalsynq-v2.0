using Commerce.Application.Catalog.Abstractions;
using Commerce.Contracts.Catalog;
using Microsoft.AspNetCore.Mvc;

namespace Commerce.Api.Controllers.Catalog;

[ApiController]
[Route("api/commerce/catalog/plans")]
public sealed class PlansController : ControllerBase
{
    private readonly IPlanCatalogService _service;
    public PlansController(IPlanCatalogService service) => _service = service;

    [HttpPost]
    [ProducesResponseType(typeof(PlanResponse), StatusCodes.Status201Created)]
    public async Task<ActionResult<PlanResponse>> Create([FromBody] CreatePlanRequest request, CancellationToken ct)
    {
        var result = await _service.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<PlanResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<PlanResponse>>> List(CancellationToken ct)
        => Ok(await _service.ListAsync(ct));

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(PlanResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<PlanResponse>> GetById(Guid id, CancellationToken ct)
        => Ok(await _service.GetAsync(id, ct));

    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(PlanResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<PlanResponse>> Update(Guid id, [FromBody] UpdatePlanRequest request, CancellationToken ct)
        => Ok(await _service.UpdateAsync(id, request, ct));

    [HttpPost("{id:guid}/activate")]
    public async Task<ActionResult<PlanResponse>> Activate(Guid id, CancellationToken ct)
        => Ok(await _service.ActivateAsync(id, ct));

    [HttpPost("{id:guid}/retire")]
    public async Task<ActionResult<PlanResponse>> Retire(Guid id, CancellationToken ct)
        => Ok(await _service.RetireAsync(id, ct));

    // ---- Plan features ----

    [HttpPost("{planId:guid}/features")]
    [ProducesResponseType(typeof(PlanFeatureResponse), StatusCodes.Status201Created)]
    public async Task<ActionResult<PlanFeatureResponse>> AddFeature(Guid planId, [FromBody] AddPlanFeatureRequest request, CancellationToken ct)
    {
        var result = await _service.AddFeatureAsync(planId, request, ct);
        return CreatedAtAction(nameof(ListFeatures), new { planId }, result);
    }

    [HttpGet("{planId:guid}/features")]
    [ProducesResponseType(typeof(IReadOnlyList<PlanFeatureResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<PlanFeatureResponse>>> ListFeatures(Guid planId, CancellationToken ct)
        => Ok(await _service.ListFeaturesAsync(planId, ct));

    [HttpDelete("{planId:guid}/features/{featureId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> RemoveFeature(Guid planId, Guid featureId, CancellationToken ct)
    {
        await _service.RemoveFeatureAsync(planId, featureId, ct);
        return NoContent();
    }
}
