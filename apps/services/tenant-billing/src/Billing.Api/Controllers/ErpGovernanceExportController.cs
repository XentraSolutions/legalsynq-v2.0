using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Net.Http.Headers;
using Billing.Api.Tenancy;
using Billing.Domain.Accounting.Erp.Governance.Export;

namespace Billing.Api.Controllers;

/// <summary>
/// MS-BILL-ERP-008 — Read-only governance evidence export.
///
/// <para>
/// Five tenant-admin GET endpoints under
/// <c>/api/erp/governance/export/*</c>. Each delegates to
/// <see cref="IGovernanceExportService"/>, which composes the
/// existing ERP-007 <see cref="IErpGovernanceAnalyticsService"/>
/// projection and serialises it into CSV or JSON.
/// </para>
///
/// <para>
/// NONE of these endpoints mutate state, enqueue work, replay an
/// export, retry a failed call, contact an ERP provider, fan out
/// a queue message, or schedule background repair. They are
/// strictly read-only file downloads.
/// </para>
///
/// <para>
/// Tenant id is resolved from <see cref="ITenantContext"/> (set
/// by the BFF-injected <c>X-Tenant-Id</c> header). Browser-
/// supplied <c>X-Tenant-Id</c> values are stripped at the BFF.
/// </para>
/// </summary>
[ApiController]
[Route("api/erp/governance/export")]
public sealed class ErpGovernanceExportController : ControllerBase
{
    private readonly IGovernanceExportService _exporter;
    private readonly ITenantContext _tenant;
    private readonly ILogger<ErpGovernanceExportController> _logger;

    public ErpGovernanceExportController(
        IGovernanceExportService exporter,
        ITenantContext tenant,
        ILogger<ErpGovernanceExportController> logger)
    {
        _exporter = exporter;
        _tenant = tenant;
        _logger = logger;
    }

    [HttpGet("summary")]
    public async Task<IActionResult> ExportSummary(
        [FromQuery] string? format,
        [FromQuery] int? windowDays,
        CancellationToken ct)
    {
        var fmt = GovernanceExportFormatParser.Parse(format);
        var payload = await _exporter
            .ExportSummaryAsync(_tenant.TenantId, windowDays, fmt, ct)
            .ConfigureAwait(false);
        Log(payload);
        return ToFile(payload);
    }

    [HttpGet("export-trends")]
    public async Task<IActionResult> ExportTrends(
        [FromQuery] string? format,
        [FromQuery] int? windowDays,
        CancellationToken ct)
    {
        var fmt = GovernanceExportFormatParser.Parse(format);
        var payload = await _exporter
            .ExportTrendsAsync(_tenant.TenantId, windowDays, fmt, ct)
            .ConfigureAwait(false);
        Log(payload);
        return ToFile(payload);
    }

    [HttpGet("remediation-aging")]
    public async Task<IActionResult> ExportRemediationAging(
        [FromQuery] string? format,
        [FromQuery] int? windowDays,
        CancellationToken ct)
    {
        var fmt = GovernanceExportFormatParser.Parse(format);
        var payload = await _exporter
            .ExportRemediationAgingAsync(_tenant.TenantId, windowDays, fmt, ct)
            .ConfigureAwait(false);
        Log(payload);
        return ToFile(payload);
    }

    [HttpGet("audit-trail")]
    public async Task<IActionResult> ExportAuditTrail(
        [FromQuery] string? format,
        [FromQuery] int? windowDays,
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        CancellationToken ct)
    {
        var fmt = GovernanceExportFormatParser.Parse(format);
        var payload = await _exporter
            .ExportAuditTrailAsync(_tenant.TenantId, windowDays, page, pageSize, fmt, ct)
            .ConfigureAwait(false);
        Log(payload);
        return ToFile(payload);
    }

    [HttpGet("drift-indicators")]
    public async Task<IActionResult> ExportDriftIndicators(
        [FromQuery] string? format,
        [FromQuery] int? windowDays,
        CancellationToken ct)
    {
        var fmt = GovernanceExportFormatParser.Parse(format);
        var payload = await _exporter
            .ExportDriftIndicatorsAsync(_tenant.TenantId, windowDays, fmt, ct)
            .ConfigureAwait(false);
        Log(payload);
        return ToFile(payload);
    }

    /// <summary>
    /// Convert an <see cref="GovernanceExportPayload"/> into an
    /// HTTP response. Surfaces the metadata envelope as
    /// <c>X-Governance-Export-*</c> response headers so an
    /// archive tool that only sees the file blob can still
    /// index it without parsing the body.
    /// </summary>
    private FileContentResult ToFile(GovernanceExportPayload payload)
    {
        Response.Headers["X-Governance-Export-Type"] = payload.Metadata.ExportType;
        Response.Headers["X-Governance-Export-Window-Days"] =
            payload.Metadata.WindowDays.ToString(System.Globalization.CultureInfo.InvariantCulture);
        Response.Headers["X-Governance-Export-Window-From-Utc"] =
            payload.Metadata.WindowFromUtc.ToString("o", System.Globalization.CultureInfo.InvariantCulture);
        Response.Headers["X-Governance-Export-Window-To-Utc"] =
            payload.Metadata.WindowToUtc.ToString("o", System.Globalization.CultureInfo.InvariantCulture);
        Response.Headers["X-Governance-Export-Generated-At-Utc"] =
            payload.Metadata.GeneratedAtUtc.ToString("o", System.Globalization.CultureInfo.InvariantCulture);
        Response.Headers["X-Governance-Export-Schema-Version"] =
            payload.Metadata.SchemaVersion.ToString(System.Globalization.CultureInfo.InvariantCulture);

        // Cache must NEVER store an export tied to a single
        // tenant + session — Vary on the auth header and forbid
        // shared caches outright.
        Response.Headers[HeaderNames.CacheControl] = "private, no-store, max-age=0";
        Response.Headers[HeaderNames.Pragma] = "no-cache";

        return new FileContentResult(payload.Body, payload.ContentType)
        {
            FileDownloadName = payload.Filename,
        };
    }

    private void Log(GovernanceExportPayload payload)
    {
        _logger.LogInformation(
            "billing_erp_governance_export tenant={TenantId} type={Type} window={Window} bytes={Bytes} contentType={ContentType}",
            _tenant.TenantId,
            payload.Metadata.ExportType,
            payload.Metadata.WindowDays,
            payload.Body.Length,
            payload.ContentType);
    }
}
