using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Billing.Api.Contracts;
using Billing.Api.Tenancy;
using Billing.Domain.Accounting.Erp.Governance;

namespace Billing.Api.Controllers;

/// <summary>
/// MS-BILL-ERP-007 — Read-only governance analytics over the
/// existing ERP-001 / ERP-003 / ERP-005 / ERP-006 surfaces.
///
/// <para>
/// Five tenant-admin GET endpoints. NONE mutate state, enqueue
/// work, replay an export, retry a failed call, contact an ERP
/// provider, fan out a queue message, or schedule background
/// repair. Aggregates are derived deterministically from
/// append-only Billing rows.
/// </para>
///
/// <para>
/// Tenant id is resolved from <see cref="ITenantContext"/> (set
/// by the BFF-injected <c>X-Tenant-Id</c> header). Browser-
/// supplied <c>X-Tenant-Id</c> values are stripped at the BFF.
/// </para>
/// </summary>
[ApiController]
[Route("api/erp/governance")]
public sealed class ErpGovernanceAnalyticsController : ControllerBase
{
    private readonly IErpGovernanceAnalyticsService _service;
    private readonly ITenantContext _tenant;
    private readonly ILogger<ErpGovernanceAnalyticsController> _logger;

    public ErpGovernanceAnalyticsController(
        IErpGovernanceAnalyticsService service,
        ITenantContext tenant,
        ILogger<ErpGovernanceAnalyticsController> logger)
    {
        _service = service;
        _tenant = tenant;
        _logger = logger;
    }

    [HttpGet("summary")]
    [ProducesResponseType(typeof(ErpGovernanceSummaryResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSummary(
        [FromQuery] int? windowDays,
        CancellationToken ct)
    {
        var summary = await _service
            .GetSummaryAsync(_tenant.TenantId, windowDays, ct)
            .ConfigureAwait(false);
        _logger.LogInformation(
            "billing_erp_governance.summary tenant={TenantId} windowDays={WindowDays} totalExports={TotalExports} unresolved={Unresolved}",
            _tenant.TenantId, summary.WindowDays, summary.TotalExports, summary.UnresolvedMappingCount);
        return Ok(ErpGovernanceSummaryResponse.From(summary));
    }

    [HttpGet("export-trends")]
    [ProducesResponseType(typeof(ErpExportTrendResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetExportTrends(
        [FromQuery] int? windowDays,
        CancellationToken ct)
    {
        var trends = await _service
            .GetExportTrendsAsync(_tenant.TenantId, windowDays, ct)
            .ConfigureAwait(false);
        _logger.LogInformation(
            "billing_erp_governance.export_trends tenant={TenantId} windowDays={WindowDays} buckets={Buckets}",
            _tenant.TenantId, trends.WindowDays, trends.Buckets.Count);
        return Ok(ErpExportTrendResponse.From(trends));
    }

    [HttpGet("remediation-aging")]
    [ProducesResponseType(typeof(RemediationAgingResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetRemediationAging(
        [FromQuery] int? windowDays,
        CancellationToken ct)
    {
        var aging = await _service
            .GetRemediationAgingAsync(_tenant.TenantId, windowDays, ct)
            .ConfigureAwait(false);
        _logger.LogInformation(
            "billing_erp_governance.remediation_aging tenant={TenantId} unresolved={Unresolved} oldest={Oldest} stale={Stale}",
            _tenant.TenantId, aging.UnresolvedCount, aging.OldestAgeDays, aging.StaleMappingCount);
        return Ok(RemediationAgingResponse.From(aging));
    }

    [HttpGet("audit-trail")]
    [ProducesResponseType(typeof(GovernanceAuditResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAuditTrail(
        [FromQuery] int? windowDays,
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        CancellationToken ct)
    {
        var audit = await _service
            .GetAuditTrailAsync(_tenant.TenantId, windowDays, page, pageSize, ct)
            .ConfigureAwait(false);
        _logger.LogInformation(
            "billing_erp_governance.audit_trail tenant={TenantId} windowDays={WindowDays} page={Page} pageSize={PageSize} total={Total}",
            _tenant.TenantId, audit.WindowDays, audit.Page, audit.PageSize, audit.TotalCount);
        return Ok(GovernanceAuditResponse.From(audit));
    }

    [HttpGet("drift-indicators")]
    [ProducesResponseType(typeof(DriftIndicatorResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDriftIndicators(
        [FromQuery] int? windowDays,
        CancellationToken ct)
    {
        var drift = await _service
            .GetDriftIndicatorsAsync(_tenant.TenantId, windowDays, ct)
            .ConfigureAwait(false);
        _logger.LogInformation(
            "billing_erp_governance.drift_indicators tenant={TenantId} windowDays={WindowDays} repeated={Repeated} stale={Stale} replay={Replay}",
            _tenant.TenantId, drift.WindowDays, drift.RepeatedFailureCount, drift.StaleMappingCount, drift.ReplayHeavyCount);
        return Ok(DriftIndicatorResponse.From(drift));
    }
}
