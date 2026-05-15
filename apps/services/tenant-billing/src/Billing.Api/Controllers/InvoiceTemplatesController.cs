using Microsoft.AspNetCore.Mvc;
using Billing.Api.Contracts;
using Billing.Api.Security;
using Billing.Api.Tenancy;
using Billing.Domain.Entities;
using Billing.Domain.Services;

namespace Billing.Api.Controllers;

/// <summary>
/// Owner-scoped REST surface for the Invoice Template & Branding
/// catalogue (INV-TPL-01). The controller exposes two parallel route
/// trees:
///
///   - <c>/api/invoice-templates/platform/...</c> — Platform Billing
///     templates. The tenant header is NOT required for these routes
///     because they live outside any tenant scope. They are not
///     wired through <see cref="TenantResolutionMiddleware"/>'s
///     protected-prefix list because that middleware short-circuits
///     ALL <c>/api/*</c> requests; instead we keep the prefix and
///     just don't read the tenant context on platform actions.
///   - <c>/api/invoice-templates/tenant/...</c> — Tenant Billing
///     templates. These call paths read <see cref="ITenantContext"/>
///     and the middleware enforces a valid <c>X-Tenant-Id</c> header.
///
/// Owner scope is NEVER taken from the request body — it is derived
/// from the route. A tenant therefore cannot create a Platform
/// template, and a platform admin cannot accidentally write into a
/// tenant's catalogue, even with a malformed payload.
/// </summary>
[ApiController]
[Route("api/invoice-templates")]
public sealed class InvoiceTemplatesController : ControllerBase
{
    private readonly IInvoiceTemplateService _service;
    private readonly ITenantContext _tenant;

    public InvoiceTemplatesController(IInvoiceTemplateService service, ITenantContext tenant)
    {
        _service = service;
        _tenant = tenant;
    }

    // =================================================================
    // Platform routes
    //
    // MS-BILL-SVC-003: every platform action is gated behind the
    // PlatformTemplatesGuard filter, which short-circuits with 404 unless
    // BILLING_ENABLE_PLATFORM_TEMPLATES=true (or Billing:EnablePlatformTemplates=true).
    // The default is OFF for the Monk Search tenant Billing scope.
    // =================================================================

    [HttpPost("platform")]
    [PlatformTemplatesGuard]
    [ProducesResponseType(typeof(InvoiceTemplateResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<IActionResult> CreatePlatform([FromBody] CreateInvoiceTemplateRequest request, CancellationToken ct)
        => Create(tenantId: null, request, ct);

    [HttpGet("platform")]
    [PlatformTemplatesGuard]
    [ProducesResponseType(typeof(IReadOnlyList<InvoiceTemplateSummaryResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<IActionResult> ListPlatform(CancellationToken ct) => List(tenantId: null, ct);

    [HttpGet("platform/{id:guid}")]
    [PlatformTemplatesGuard]
    [ProducesResponseType(typeof(InvoiceTemplateResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public Task<IActionResult> GetPlatform(Guid id, CancellationToken ct) => Get(tenantId: null, id, ct);

    [HttpPut("platform/{id:guid}")]
    [PlatformTemplatesGuard]
    [ProducesResponseType(typeof(InvoiceTemplateResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public Task<IActionResult> UpdatePlatform(Guid id, [FromBody] UpdateInvoiceTemplateRequest request, CancellationToken ct)
        => Update(tenantId: null, id, request, ct);

    [HttpPost("platform/{id:guid}/activate")]
    [PlatformTemplatesGuard]
    [ProducesResponseType(typeof(InvoiceTemplateResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public Task<IActionResult> ActivatePlatform(Guid id, CancellationToken ct) => Activate(tenantId: null, id, ct);

    [HttpPost("platform/{id:guid}/retire")]
    [PlatformTemplatesGuard]
    [ProducesResponseType(typeof(InvoiceTemplateResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public Task<IActionResult> RetirePlatform(Guid id, CancellationToken ct) => Retire(tenantId: null, id, ct);

    [HttpPost("platform/{id:guid}/make-default")]
    [PlatformTemplatesGuard]
    [ProducesResponseType(typeof(MakeDefaultTemplateResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public Task<IActionResult> MakeDefaultPlatform(Guid id, CancellationToken ct) => MakeDefault(tenantId: null, id, ct);

    [HttpGet("platform/default")]
    [PlatformTemplatesGuard]
    [ProducesResponseType(typeof(InvoiceTemplateResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<IActionResult> GetDefaultPlatform(CancellationToken ct) => GetDefault(tenantId: null, ct);

    // =================================================================
    // Tenant routes — use _tenant.TenantId for scope
    // =================================================================

    [HttpPost("tenant")]
    [RequireTenantBillingAccess(TenantBillingOperationCategory.TemplateWrite)]
    [ProducesResponseType(typeof(InvoiceTemplateResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public Task<IActionResult> CreateTenant([FromBody] CreateInvoiceTemplateRequest request, CancellationToken ct)
        => Create(tenantId: _tenant.TenantId, request, ct);

    [HttpGet("tenant")]
    [ProducesResponseType(typeof(IReadOnlyList<InvoiceTemplateSummaryResponse>), StatusCodes.Status200OK)]
    public Task<IActionResult> ListTenant(CancellationToken ct) => List(tenantId: _tenant.TenantId, ct);

    [HttpGet("tenant/{id:guid}")]
    [ProducesResponseType(typeof(InvoiceTemplateResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public Task<IActionResult> GetTenant(Guid id, CancellationToken ct) => Get(tenantId: _tenant.TenantId, id, ct);

    [HttpPut("tenant/{id:guid}")]
    [RequireTenantBillingAccess(TenantBillingOperationCategory.TemplateWrite)]
    [ProducesResponseType(typeof(InvoiceTemplateResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public Task<IActionResult> UpdateTenant(Guid id, [FromBody] UpdateInvoiceTemplateRequest request, CancellationToken ct)
        => Update(tenantId: _tenant.TenantId, id, request, ct);

    [HttpPost("tenant/{id:guid}/activate")]
    [RequireTenantBillingAccess(TenantBillingOperationCategory.TemplateWrite)]
    [ProducesResponseType(typeof(InvoiceTemplateResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public Task<IActionResult> ActivateTenant(Guid id, CancellationToken ct) => Activate(tenantId: _tenant.TenantId, id, ct);

    [HttpPost("tenant/{id:guid}/retire")]
    [RequireTenantBillingAccess(TenantBillingOperationCategory.TemplateWrite)]
    [ProducesResponseType(typeof(InvoiceTemplateResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public Task<IActionResult> RetireTenant(Guid id, CancellationToken ct) => Retire(tenantId: _tenant.TenantId, id, ct);

    [HttpPost("tenant/{id:guid}/make-default")]
    [RequireTenantBillingAccess(TenantBillingOperationCategory.TemplateWrite)]
    [ProducesResponseType(typeof(MakeDefaultTemplateResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public Task<IActionResult> MakeDefaultTenant(Guid id, CancellationToken ct) => MakeDefault(tenantId: _tenant.TenantId, id, ct);

    [HttpGet("tenant/default")]
    [ProducesResponseType(typeof(InvoiceTemplateResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<IActionResult> GetDefaultTenant(CancellationToken ct) => GetDefault(tenantId: _tenant.TenantId, ct);

    // =================================================================
    // Shared private helpers
    // =================================================================

    private async Task<IActionResult> Create(Guid? tenantId, CreateInvoiceTemplateRequest request, CancellationToken ct)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        try
        {
            var created = await _service.CreateAsync(tenantId, request.ToCommand(), ct);
            return CreatedAtAction(
                tenantId is null ? nameof(GetPlatform) : nameof(GetTenant),
                new { id = created.Id },
                InvoiceTemplateResponse.From(created));
        }
        catch (ArgumentException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }
        catch (InvoiceTemplateDefaultConflictException ex)
        {
            // Concurrent default promotion (or auto-default on first
            // Active) lost the race against another writer. Surface as
            // 409 Conflict so callers can retry; must come BEFORE the
            // generic InvalidOperationException catch since it inherits
            // from it.
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status409Conflict);
        }
        catch (InvalidOperationException ex)
        {
            // Catches all other InvoiceTemplateException subclasses,
            // which inherit InvalidOperationException.
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }
    }

    private async Task<IActionResult> List(Guid? tenantId, CancellationToken ct)
    {
        var items = await _service.ListAsync(tenantId, ct);
        return Ok(items.Select(InvoiceTemplateSummaryResponse.From).ToList());
    }

    private async Task<IActionResult> Get(Guid? tenantId, Guid id, CancellationToken ct)
    {
        if (id == Guid.Empty)
            return Problem(detail: "Template id is required.", statusCode: StatusCodes.Status400BadRequest);

        try
        {
            var t = await _service.GetAsync(tenantId, id, ct);
            return t is null ? NotFound() : Ok(InvoiceTemplateResponse.From(t));
        }
        catch (ArgumentException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }
    }

    private async Task<IActionResult> GetDefault(Guid? tenantId, CancellationToken ct)
    {
        var t = await _service.GetDefaultAsync(tenantId, ct);
        return t is null ? NotFound() : Ok(InvoiceTemplateResponse.From(t));
    }

    private async Task<IActionResult> Update(Guid? tenantId, Guid id, UpdateInvoiceTemplateRequest request, CancellationToken ct)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        if (id == Guid.Empty)
            return Problem(detail: "Template id is required.", statusCode: StatusCodes.Status400BadRequest);

        try
        {
            var updated = await _service.UpdateAsync(tenantId, id, request.ToCommand(), ct);
            return updated is null ? NotFound() : Ok(InvoiceTemplateResponse.From(updated));
        }
        catch (ArgumentException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }
        catch (InvoiceTemplateDefaultConflictException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status409Conflict);
        }
        catch (InvalidOperationException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }
    }

    private async Task<IActionResult> Activate(Guid? tenantId, Guid id, CancellationToken ct)
    {
        if (id == Guid.Empty)
            return Problem(detail: "Template id is required.", statusCode: StatusCodes.Status400BadRequest);

        try
        {
            var t = await _service.ActivateAsync(tenantId, id, ct);
            return t is null ? NotFound() : Ok(InvoiceTemplateResponse.From(t));
        }
        catch (InvalidOperationException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }
    }

    private async Task<IActionResult> Retire(Guid? tenantId, Guid id, CancellationToken ct)
    {
        if (id == Guid.Empty)
            return Problem(detail: "Template id is required.", statusCode: StatusCodes.Status400BadRequest);

        try
        {
            var t = await _service.RetireAsync(tenantId, id, ct);
            return t is null ? NotFound() : Ok(InvoiceTemplateResponse.From(t));
        }
        catch (InvalidOperationException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }
    }

    private async Task<IActionResult> MakeDefault(Guid? tenantId, Guid id, CancellationToken ct)
    {
        if (id == Guid.Empty)
            return Problem(detail: "Template id is required.", statusCode: StatusCodes.Status400BadRequest);

        // Capture the previous default (if any) before the transition
        // so the response can echo it back to the caller. The lookup
        // is read-only and tenant-scoped via the same scope rule.
        InvoiceTemplate? previousDefault = null;
        try
        {
            previousDefault = await _service.GetDefaultAsync(tenantId, ct);
        }
        catch (ArgumentException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }

        try
        {
            var t = await _service.MakeDefaultAsync(tenantId, id, ct);
            if (t is null) return NotFound();

            return Ok(new MakeDefaultTemplateResponse(
                InvoiceTemplateResponse.From(t),
                // If the new default IS the previous default (idempotent
                // call), don't echo a stale "previous" id. Same-scope
                // means same id-set, so equality on Id is safe here.
                previousDefault?.Id == t.Id ? null : previousDefault?.Id));
        }
        catch (InvoiceTemplateDefaultConflictException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status409Conflict);
        }
        catch (InvalidOperationException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }
    }
}
