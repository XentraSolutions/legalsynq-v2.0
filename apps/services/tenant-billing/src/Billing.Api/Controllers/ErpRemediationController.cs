using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Billing.Api.Contracts;
using Billing.Api.Tenancy;
using Billing.Domain.Accounting.Erp.Remediation;

namespace Billing.Api.Controllers;

/// <summary>
/// MS-BILL-ERP-005 — Tenant-admin-only ERP mapping-remediation
/// surface. Three read-only routes that let an operator surface
/// unmapped Billing customers, search QBO server-side for the
/// right counterpart, and validate a candidate mapping BEFORE
/// confirming it via the existing ERP-003 POST.
///
/// <para>
/// Routes (tenant-scoped; trusted tenant id from
/// <see cref="ITenantContext"/>, NEVER the request body or query):
/// </para>
/// <list type="bullet">
///   <item><c>GET  /api/erp/quickbooks/unmapped-customers</c></item>
///   <item><c>GET  /api/erp/quickbooks/customer-search?q=…</c></item>
///   <item><c>POST /api/erp/quickbooks/customer-mappings/validate</c></item>
/// </list>
///
/// <para>
/// Authorization mirrors the rest of the ERP surface: the BFF
/// allowlist gates every entry with <c>requireAdminSession</c>; the
/// validate POST is read-only so it does NOT carry
/// <c>requireOriginAllowlist / requireIdempotencyKey</c> — those
/// gates exist for double-submit protection on writes. The actual
/// persistence step REUSES the existing ERP-003 POST, which
/// continues to carry the full write-protection bundle.
/// </para>
///
/// <para>
/// Forbidden surfaces explicitly NOT exposed: no QBO customer
/// create, no automatic mapping, no fuzzy-match endpoint, no
/// bulk-import, no resolver bypass. The QBO token is NEVER returned
/// to the browser — every QBO call happens server-side via
/// <see cref="IQuickBooksCustomerLookup"/>.
/// </para>
/// </summary>
[ApiController]
[Route("api/erp/quickbooks")]
public sealed class ErpRemediationController : ControllerBase
{
    private const int SearchQueryMaxLength = 80;

    private readonly IErpRemediationService _service;
    private readonly ITenantContext _tenant;
    private readonly ILogger<ErpRemediationController> _logger;

    public ErpRemediationController(
        IErpRemediationService service,
        ITenantContext tenant,
        ILogger<ErpRemediationController> logger)
    {
        _service = service;
        _tenant = tenant;
        _logger = logger;
    }

    [HttpGet("unmapped-customers")]
    public async Task<IActionResult> ListUnmappedCustomers(CancellationToken ct)
    {
        var rows = await _service.ListUnmappedCustomersAsync(_tenant.TenantId, ct).ConfigureAwait(false);
        var items = rows.Select(UnmappedCustomerRowResponse.From).ToList();
        return Ok(new UnmappedCustomerListResponse(items.Count, items));
    }

    [HttpGet("customer-search")]
    public async Task<IActionResult> SearchCustomers(
        [FromQuery(Name = "q")] string? q,
        CancellationToken ct)
    {
        // Force tenant-context resolution even though the QBO
        // realm is configured globally for the Billing service:
        // this guarantees the request is bound to a logged-in
        // tenant-admin session (ITenantContext throws if the IDM
        // session header is missing) and gives the correlation
        // log a tenant id alongside the QBO call. The browser is
        // never trusted to supply X-Tenant-Id; the BFF strips it
        // and the IDM-injected header is the only source.
        var tenantId = _tenant.TenantId;
        var query = (q ?? string.Empty).Trim();
        if (query.Length > SearchQueryMaxLength)
        {
            query = query.Substring(0, SearchQueryMaxLength);
        }
        _logger.LogInformation(
            "ERP-005 customer-search invoked by tenant {TenantId} with query length {Length}",
            tenantId, query.Length);
        var result = await _service.SearchQuickBooksCustomersAsync(query, ct).ConfigureAwait(false);
        return Ok(QuickBooksCustomerSearchResponse.From(result));
    }

    [HttpPost("customer-mappings/validate")]
    public async Task<IActionResult> ValidateMapping(
        [FromBody] ValidateMappingRequestBody body,
        CancellationToken ct)
    {
        if (body is null)
            return BadRequest(new { error = "Request body is required." });
        if (!ModelState.IsValid) return ValidationProblem(ModelState);

        var command = new MappingValidationCommand(
            BillingCustomerId: body.BillingCustomerId,
            QuickBooksCustomerId: body.QuickBooksCustomerId);
        var result = await _service
            .ValidateMappingAsync(_tenant.TenantId, command, ct)
            .ConfigureAwait(false);
        return Ok(MappingValidationResponse.From(result));
    }
}
