using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Billing.Api.Contracts;
using Billing.Api.Tenancy;
using Billing.Domain.Accounting.Erp.Reconciliation;

namespace Billing.Api.Controllers;

/// <summary>
/// MS-BILL-ERP-004 — Tenant-admin-only ERP reconciliation /
/// diagnostics controller. Pure read surface — every route is a
/// GET, every handler is a deterministic projection over the
/// existing append-only Billing tables.
///
/// <para>
/// Routes:
/// <list type="bullet">
///   <item><c>GET /api/erp/reconciliation/summary</c></item>
///   <item><c>GET /api/erp/reconciliation/exports</c></item>
///   <item><c>GET /api/erp/reconciliation/exports/{id}</c></item>
///   <item><c>GET /api/erp/reconciliation/mapping-health</c></item>
///   <item><c>GET /api/erp/reconciliation/provider-health</c></item>
/// </list>
/// </para>
///
/// <para>
/// Authorization posture mirrors the existing OPS-002 / WRITE-007
/// read endpoints: BFF-side <c>requireAdminSession</c> + tenant id
/// from <see cref="ITenantContext.TenantId"/>. Browser-supplied
/// <c>X-Tenant-Id</c> is forbidden; cross-tenant probes resolve to
/// <c>404 NotFound</c>, never <c>403</c>, so the id-existence
/// channel does not leak.
/// </para>
///
/// <para>
/// Forbidden surfaces explicitly NOT exposed: no retry / replay
/// trigger, no payload mutation, no provider call, no QuickBooks
/// secret / token exposure, no scheduled trigger, no callback into
/// Billing.Api.
/// </para>
/// </summary>
[ApiController]
[Route("api/erp/reconciliation")]
public sealed class ErpReconciliationController : ControllerBase
{
    private readonly IErpReconciliationService _reconciliation;
    private readonly ITenantContext _tenant;
    private readonly ILogger<ErpReconciliationController> _logger;

    public ErpReconciliationController(
        IErpReconciliationService reconciliation,
        ITenantContext tenant,
        ILogger<ErpReconciliationController> logger)
    {
        _reconciliation = reconciliation;
        _tenant = tenant;
        _logger = logger;
    }

    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary(CancellationToken ct)
    {
        var s = await _reconciliation
            .GetSummaryAsync(_tenant.TenantId, ct)
            .ConfigureAwait(false);
        return Ok(ErpReconciliationSummaryResponse.FromDomain(s));
    }

    [HttpGet("exports")]
    public async Task<IActionResult> ListExports(
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        [FromQuery] string? status,
        [FromQuery] string? provider,
        CancellationToken ct)
    {
        var p = page ?? 1;
        var ps = pageSize ?? ErpReconciliationService.DefaultPageSize;

        var query = new ErpReconciliationListQuery(p, ps, status, provider);
        var rows = await _reconciliation
            .ListExportsAsync(_tenant.TenantId, query, ct)
            .ConfigureAwait(false);

        var items = rows.Select(ErpExportDiagnosticResponse.FromDomain).ToList();
        return Ok(new ErpExportDiagnosticListResponse(
            Page: p < 1 ? 1 : p,
            PageSize: ps < 1 ? ErpReconciliationService.DefaultPageSize
                     : (ps > ErpReconciliationService.MaxPageSize
                            ? ErpReconciliationService.MaxPageSize : ps),
            Count: items.Count,
            Items: items));
    }

    [HttpGet("exports/{id:guid}")]
    public async Task<IActionResult> GetExportDetail(Guid id, CancellationToken ct)
    {
        var detail = await _reconciliation
            .GetExportDetailAsync(_tenant.TenantId, id, ct)
            .ConfigureAwait(false);
        if (detail is null) return NotFound();
        return Ok(ErpExportDiagnosticDetailResponse.FromDomain(detail));
    }

    [HttpGet("mapping-health")]
    public async Task<IActionResult> GetMappingHealth(CancellationToken ct)
    {
        var s = await _reconciliation
            .GetMappingHealthAsync(_tenant.TenantId, ct)
            .ConfigureAwait(false);
        return Ok(ErpMappingHealthResponse.FromDomain(s));
    }

    [HttpGet("provider-health")]
    public async Task<IActionResult> GetProviderHealth(CancellationToken ct)
    {
        var s = await _reconciliation
            .GetProviderHealthAsync(_tenant.TenantId, ct)
            .ConfigureAwait(false);
        return Ok(ErpProviderHealthResponse.FromDomain(s));
    }
}
