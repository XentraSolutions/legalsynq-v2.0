using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Billing.Api.Contracts;
using Billing.Api.Security;
using Billing.Api.Tenancy;
using Billing.Domain.Accounting.Erp.BulkImport;
using Billing.Domain.Services;

namespace Billing.Api.Controllers;

/// <summary>
/// MS-BILL-ERP-006 — Tenant-admin-only bulk import / export /
/// history surface for the operator-curated Billing↔QBO customer
/// mapping. Sits ALONGSIDE the existing
/// <see cref="QuickBooksCustomerMappingsController"/> CRUD surface
/// — every per-row write still funnels through ERP-003's
/// <c>POST /api/erp/quickbooks/customer-mappings</c> path and its
/// unique-index 409 backstop applies on every commit row.
///
/// <para>
/// Routes (tenant-scoped; trusted tenant id from
/// <see cref="ITenantContext"/>, NEVER the request body):
/// </para>
/// <list type="bullet">
///   <item><c>POST /api/erp/quickbooks/customer-mappings/import/validate</c>
///         (multipart/form-data, <c>file</c> field)</item>
///   <item><c>POST /api/erp/quickbooks/customer-mappings/import/commit</c>
///         (JSON body, <c>Idempotency-Key</c> required)</item>
///   <item><c>GET  /api/erp/quickbooks/customer-mappings/export</c></item>
///   <item><c>GET  /api/erp/quickbooks/customer-mappings/import/history</c></item>
/// </list>
///
/// <para>
/// Forbidden surfaces explicitly NOT exposed: no automatic mapping
/// creation, no fuzzy matching, no AI inference, no QBO customer
/// creation, no replay/retry execution, no bidirectional sync, no
/// webhook mutation handler, no scheduled import job. The CSV
/// upload is parsed entirely server-side; the browser never sends
/// a "pre-parsed" rows JSON to the validate endpoint.
/// </para>
/// </summary>
[ApiController]
[Route("api/erp/quickbooks/customer-mappings")]
public sealed class BulkMappingImportController : ControllerBase
{
    private const long MaxUploadBytes = 1L * 1024 * 1024;
    private const string ExportContentType = "text/csv";
    private const string ExportFileName = "quickbooks-customer-mappings.csv";

    private readonly IBulkMappingImportService _service;
    private readonly ITenantContext _tenant;
    private readonly ILogger<BulkMappingImportController> _logger;

    public BulkMappingImportController(
        IBulkMappingImportService service,
        ITenantContext tenant,
        ILogger<BulkMappingImportController> logger)
    {
        _service = service;
        _tenant = tenant;
        _logger = logger;
    }

    [HttpPost("import/validate")]
    [RequestSizeLimit(MaxUploadBytes)]
    public async Task<IActionResult> ValidateImport(
        [FromForm(Name = "file")] IFormFile? file,
        CancellationToken ct)
    {
        if (file is null || file.Length == 0)
        {
            return BadRequest(new { error = "A CSV file is required (multipart/form-data field 'file')." });
        }
        if (file.Length > MaxUploadBytes)
        {
            return BadRequest(new { error = $"CSV body exceeds the {MaxUploadBytes / 1024} KB limit." });
        }

        await using var stream = file.OpenReadStream();
        var preview = await _service.ValidateAsync(_tenant.TenantId, stream, ct).ConfigureAwait(false);
        _logger.LogInformation(
            "ERP-006 import.validate tenantId={TenantId} previewToken={PreviewToken} total={Total} valid={Valid} warning={Warning} rejected={Rejected}",
            _tenant.TenantId, preview.PreviewToken, preview.TotalRows,
            preview.ValidCount, preview.WarningCount, preview.RejectedCount);
        return Ok(BulkImportPreviewResponse.From(preview));
    }

    [HttpPost("import/commit")]
    [RequireTenantBillingAccess(TenantBillingOperationCategory.TemplateWrite)]
    public async Task<IActionResult> CommitImport(
        [FromBody] BulkImportCommitRequestBody body,
        CancellationToken ct)
    {
        if (body is null)
            return BadRequest(new { error = "Request body is required." });
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        var idempotencyKey = ReadIdempotencyKey();
        if (string.IsNullOrWhiteSpace(idempotencyKey))
            return BadRequest(new { error = "Idempotency-Key header is required." });

        var actor = ReadActor();
        try
        {
            var command = new BulkImportCommitCommand(
                PreviewToken: body.PreviewToken,
                Rows: body.Rows.Select(r => new BulkImportCommitRowCommand(
                    LineNumber: r.LineNumber,
                    BillingCustomerId: r.BillingCustomerId,
                    QuickBooksCustomerId: r.QuickBooksCustomerId,
                    QuickBooksDisplayName: r.QuickBooksDisplayName,
                    ExportMode: r.ExportMode,
                    Notes: r.Notes)).ToList());

            var result = await _service
                .CommitAsync(_tenant.TenantId, command, actor, idempotencyKey, ct)
                .ConfigureAwait(false);

            _logger.LogInformation(
                "ERP-006 import.commit tenantId={TenantId} historyId={HistoryId} requested={Requested} persisted={Persisted} conflicted={Conflicted} rejected={Rejected} failed={Failed}",
                _tenant.TenantId, result.HistoryId, result.TotalRequested,
                result.Persisted, result.Conflicted, result.Rejected, result.Failed);

            return Ok(BulkImportCommitResponse.From(result));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("export")]
    public async Task<IActionResult> ExportMappings(CancellationToken ct)
    {
        var bytes = await _service.ExportMappingsAsync(_tenant.TenantId, ct).ConfigureAwait(false);
        _logger.LogInformation(
            "ERP-006 export tenantId={TenantId} bytes={Bytes}",
            _tenant.TenantId, bytes.Length);
        return File(bytes, ExportContentType, ExportFileName);
    }

    [HttpGet("import/history")]
    public async Task<IActionResult> ListHistory(
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        CancellationToken ct)
    {
        var p = page is int pv && pv > 0 ? pv : 1;
        var ps = pageSize is int psv && psv > 0
            ? psv
            : BulkMappingImportService.DefaultHistoryPageSize;
        var rows = await _service
            .ListHistoryAsync(_tenant.TenantId, p, ps, ct)
            .ConfigureAwait(false);
        var items = rows.Select(BulkImportHistoryRowResponse.From).ToList();
        return Ok(new BulkImportHistoryListResponse(items.Count, items));
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
