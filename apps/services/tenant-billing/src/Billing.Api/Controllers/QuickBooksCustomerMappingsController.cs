using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Billing.Api.Contracts;
using Billing.Api.Security;
using Billing.Api.Tenancy;
using Billing.Domain.Accounting.Erp.QuickBooks;
using Billing.Domain.Services;

namespace Billing.Api.Controllers;

/// <summary>
/// MS-BILL-ERP-003 — Tenant-admin-only CRUD surface for the
/// operator-curated Billing↔QuickBooks customer mapping.
///
/// <para>
/// Routes (tenant-scoped; the trusted tenant id comes from
/// <see cref="ITenantContext"/>, NEVER the request body):
/// </para>
/// <list type="bullet">
///   <item><c>GET    /api/erp/quickbooks/customer-mappings</c></item>
///   <item><c>GET    /api/erp/quickbooks/customer-mappings/{id}</c></item>
///   <item><c>POST   /api/erp/quickbooks/customer-mappings</c></item>
///   <item><c>PUT    /api/erp/quickbooks/customer-mappings/{id}</c></item>
///   <item><c>DELETE /api/erp/quickbooks/customer-mappings/{id}</c></item>
/// </list>
///
/// <para>
/// Authorization posture mirrors ERP-001:
/// browser-supplied <c>X-Tenant-Id</c> is forbidden (BFF strips it);
/// the BFF allowlist enforces <c>requireAdminSession</c> on every
/// route plus <c>requireOriginAllowlist + requireIdempotencyKey</c>
/// on writes. Cross-tenant probes yield 404 (never 403) because the
/// repository's tenant filter never returns the row.
/// </para>
///
/// <para>
/// Forbidden surfaces explicitly NOT exposed: no QBO customer
/// search, no QBO customer create, no fuzzy-match endpoint, no
/// bulk-import. This controller never calls QuickBooks itself.
/// </para>
/// </summary>
[ApiController]
[Route("api/erp/quickbooks/customer-mappings")]
public sealed class QuickBooksCustomerMappingsController : ControllerBase
{
    private const int MaxPageSize = 100;
    private const int DefaultPageSize = 25;

    private readonly IQuickBooksCustomerMappingService _service;
    private readonly ITenantContext _tenant;
    private readonly ILogger<QuickBooksCustomerMappingsController> _logger;

    public QuickBooksCustomerMappingsController(
        IQuickBooksCustomerMappingService service,
        ITenantContext tenant,
        ILogger<QuickBooksCustomerMappingsController> logger)
    {
        _service = service;
        _tenant = tenant;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        CancellationToken ct)
    {
        var p = page is int pv && pv > 0 ? pv : 1;
        var ps = pageSize is int psv && psv > 0 ? psv : DefaultPageSize;
        if (ps > MaxPageSize) ps = MaxPageSize;

        var rows = await _service.ListAsync(_tenant.TenantId, p, ps, ct).ConfigureAwait(false);
        var items = rows.Select(QuickBooksCustomerMappingResponse.From).ToList();
        return Ok(new QuickBooksCustomerMappingListResponse(p, ps, items.Count, items));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var row = await _service.GetAsync(_tenant.TenantId, id, ct).ConfigureAwait(false);
        if (row is null) return NotFound();
        return Ok(QuickBooksCustomerMappingResponse.From(row));
    }

    [HttpPost]
    [RequireTenantBillingAccess(TenantBillingOperationCategory.TemplateWrite)]
    public async Task<IActionResult> Create(
        [FromBody] CreateQuickBooksCustomerMappingRequestBody body,
        CancellationToken ct)
    {
        if (body is null)
            return BadRequest(new { error = "Request body is required." });
        if (!ModelState.IsValid) return ValidationProblem(ModelState);

        var idempotencyKey = ReadIdempotencyKey();
        if (string.IsNullOrWhiteSpace(idempotencyKey))
            return BadRequest(new { error = "Idempotency-Key header is required." });

        var actor = ReadActor();
        try
        {
            var created = await _service.CreateAsync(
                _tenant.TenantId,
                new CreateQuickBooksCustomerMappingCommand(
                    BillingCustomerId: body.BillingCustomerId,
                    QuickBooksCustomerId: body.QuickBooksCustomerId,
                    QuickBooksDisplayName: body.QuickBooksDisplayName,
                    MappingStatus: body.MappingStatus,
                    ExportMode: body.ExportMode),
                actor,
                ct).ConfigureAwait(false);

            _logger.LogInformation(
                "qb_customer_mapping.create tenantId={TenantId} mappingId={MappingId} billingCustomerId={BillingCustomerId}",
                _tenant.TenantId, created.Id, created.BillingCustomerId);

            return CreatedAtAction(nameof(Get), new { id = created.Id },
                QuickBooksCustomerMappingResponse.From(created));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (QuickBooksCustomerMappingConflictException ex)
        {
            return Conflict(new { error = ex.Message });
        }
    }

    [HttpPut("{id:guid}")]
    [RequireTenantBillingAccess(TenantBillingOperationCategory.TemplateWrite)]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateQuickBooksCustomerMappingRequestBody body,
        CancellationToken ct)
    {
        if (body is null)
            return BadRequest(new { error = "Request body is required." });
        if (!ModelState.IsValid) return ValidationProblem(ModelState);

        var idempotencyKey = ReadIdempotencyKey();
        if (string.IsNullOrWhiteSpace(idempotencyKey))
            return BadRequest(new { error = "Idempotency-Key header is required." });

        try
        {
            var updated = await _service.UpdateAsync(
                _tenant.TenantId,
                id,
                new UpdateQuickBooksCustomerMappingCommand(
                    QuickBooksCustomerId: body.QuickBooksCustomerId,
                    QuickBooksDisplayName: body.QuickBooksDisplayName,
                    MappingStatus: body.MappingStatus,
                    ExportMode: body.ExportMode),
                ct).ConfigureAwait(false);

            _logger.LogInformation(
                "qb_customer_mapping.update tenantId={TenantId} mappingId={MappingId}",
                _tenant.TenantId, updated.Id);

            return Ok(QuickBooksCustomerMappingResponse.From(updated));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (QuickBooksCustomerMappingConflictException ex)
        {
            return Conflict(new { error = ex.Message });
        }
    }

    [HttpDelete("{id:guid}")]
    [RequireTenantBillingAccess(TenantBillingOperationCategory.TemplateWrite)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var idempotencyKey = ReadIdempotencyKey();
        if (string.IsNullOrWhiteSpace(idempotencyKey))
            return BadRequest(new { error = "Idempotency-Key header is required." });

        var removed = await _service.DeleteAsync(_tenant.TenantId, id, ct).ConfigureAwait(false);
        if (!removed) return NotFound();

        _logger.LogInformation(
            "qb_customer_mapping.delete tenantId={TenantId} mappingId={MappingId}",
            _tenant.TenantId, id);
        return NoContent();
    }

    private string ReadIdempotencyKey()
        => Request.Headers.TryGetValue("Idempotency-Key", out var v) ? v.ToString() : string.Empty;

    private string ReadActor()
    {
        if (Request.Headers.TryGetValue("X-User-DisplayName", out var v) && !string.IsNullOrWhiteSpace(v))
            return v.ToString();
        return "tenant-admin";
    }
}
