using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Billing.Api.Contracts;
using Billing.Api.Security;
using Billing.Api.Tenancy;
using Billing.Domain.Accounting.Erp;
using Billing.Domain.Services;

namespace Billing.Api.Controllers;

/// <summary>
/// MS-BILL-ERP-001 — Tenant-admin-only ERP export controller.
///
/// <para>
/// Routes:
/// <list type="bullet">
///   <item><c>POST /api/erp/exports/run</c> — trigger a new export.</item>
///   <item><c>GET  /api/erp/exports</c> — list export history (newest first).</item>
///   <item><c>GET  /api/erp/exports/{id}</c> — single export detail.</item>
///   <item><c>GET  /api/erp/exports/{id}/payload</c> — canonical payload JSON.</item>
/// </list>
/// </para>
///
/// <para>
/// Authorization posture:
/// <list type="bullet">
///   <item>Browser-supplied <c>X-Tenant-Id</c> is forbidden — the
///     BFF strips it. This controller reads the trusted tenant id
///     from <see cref="ITenantContext.TenantId"/>.</item>
///   <item>The BFF allowlist additionally enforces
///     <c>requireAdminSession</c> on every route, plus
///     <c>requireOriginAllowlist</c> + <c>requireIdempotencyKey</c>
///     on the POST.</item>
///   <item>Cross-tenant probes (e.g. fetching another tenant's
///     export id) yield <c>404 NotFound</c>, not <c>403</c> — the
///     repository's tenant filter never returns the row, so the
///     id-existence channel does not leak.</item>
/// </list>
/// </para>
///
/// <para>
/// Forbidden surfaces explicitly NOT exposed:
/// no QuickBooks / NetSuite call, no scheduled trigger, no queue
/// emission, no callback into Billing.Api, no mutation of any
/// invoice / payment / adjustment row.
/// </para>
/// </summary>
[ApiController]
[Route("api/erp/exports")]
public sealed class AccountingExportController : ControllerBase
{
    /// <summary>Hard cap on listing page size.</summary>
    private const int MaxPageSize = 100;
    private const int DefaultPageSize = 25;

    private readonly IAccountingExportService _exports;
    private readonly ITenantContext _tenant;
    private readonly ILogger<AccountingExportController> _logger;

    public AccountingExportController(
        IAccountingExportService exports,
        ITenantContext tenant,
        ILogger<AccountingExportController> logger)
    {
        _exports = exports;
        _tenant = tenant;
        _logger = logger;
    }

    /// <summary>
    /// Trigger a new export attempt. The Idempotency-Key header is
    /// required by the BFF; we mirror it into the persisted
    /// lifecycle row and into the dedupe fingerprint.
    /// </summary>
    [HttpPost("run")]
    [RequireTenantBillingAccess(TenantBillingOperationCategory.ExportWrite)]
    public async Task<IActionResult> Run(
        [FromBody] AccountingExportRunRequestBody body,
        CancellationToken ct)
    {
        if (body is null)
            return BadRequest(new { error = "Request body is required." });

        var idempotencyKey = ReadIdempotencyKey();
        if (string.IsNullOrWhiteSpace(idempotencyKey))
            return BadRequest(new { error = "Idempotency-Key header is required." });

        var requestedBy = ReadRequestedBy();

        try
        {
            var wrapped = TenantScopedAccountingExportRunRequest.Wrap(
                _tenant.TenantId,
                new AccountingExportRunRequest(
                    Provider: body.Provider,
                    ExportType: body.ExportType,
                    WindowFromUtc: body.WindowFromUtc,
                    WindowToUtc: body.WindowToUtc,
                    IdempotencyKey: idempotencyKey,
                    RequestedBy: requestedBy,
                    Reason: body.Reason));

            var result = await _exports.RunAsync(wrapped, ct).ConfigureAwait(false);

            _logger.LogInformation(
                "accounting_export.run tenantId={TenantId} provider={Provider} status={Status} duplicate={WasDuplicate} exportId={ExportId}",
                _tenant.TenantId, result.Provider, result.Status, result.WasDuplicate, result.ExportId);

            var response = AccountingExportResponse.FromRunResult(
                result, requestedBy, body.WindowFromUtc, body.WindowToUtc, body.Reason);
            return Ok(response);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Page the export history (newest first). Tenant-scoped; never
    /// returns rows from other tenants.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        CancellationToken ct)
    {
        var p = page is int pv && pv > 0 ? pv : 1;
        var ps = pageSize is int psv && psv > 0 ? psv : DefaultPageSize;
        if (ps > MaxPageSize) ps = MaxPageSize;

        var rows = await _exports.ListAsync(_tenant.TenantId, p, ps, ct).ConfigureAwait(false);
        var items = rows.Select(AccountingExportResponse.FromEntity).ToList();
        return Ok(new AccountingExportListResponse(
            Page: p, PageSize: ps, Count: items.Count, Items: items));
    }

    /// <summary>
    /// Single export detail. Cross-tenant probe → 404, never 403.
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var e = await _exports.GetAsync(_tenant.TenantId, id, ct).ConfigureAwait(false);
        if (e is null) return NotFound();
        return Ok(AccountingExportResponse.FromEntity(e));
    }

    /// <summary>
    /// Canonical server-built payload JSON for the export. The
    /// payload is itself derived from already-immutable Billing
    /// rows, so the response is replay-safe.
    /// </summary>
    [HttpGet("{id:guid}/payload")]
    public async Task<IActionResult> GetPayload(Guid id, CancellationToken ct)
    {
        var e = await _exports.GetAsync(_tenant.TenantId, id, ct).ConfigureAwait(false);
        if (e is null) return NotFound();
        return Ok(new AccountingExportPayloadResponse(
            ExportId: e.Id,
            Provider: e.Provider,
            Status: e.Status,
            ExternalReferenceId: e.ExternalReferenceId,
            CompletedAtUtc: e.CompletedAtUtc,
            PayloadJson: e.PayloadJson ?? "null"));
    }

    // ----------------------------------------------------------------

    private string ReadIdempotencyKey()
    {
        if (Request.Headers.TryGetValue("Idempotency-Key", out var v))
            return v.ToString();
        return string.Empty;
    }

    /// <summary>
    /// Best-effort caller display name from the BFF-injected
    /// <c>X-User-DisplayName</c> header; falls back to a stable
    /// literal so the row is never empty. Never reads from a
    /// browser-supplied tenant or email header.
    /// </summary>
    private string ReadRequestedBy()
    {
        if (Request.Headers.TryGetValue("X-User-DisplayName", out var v) && !string.IsNullOrWhiteSpace(v))
            return v.ToString();
        return "tenant-admin";
    }
}
