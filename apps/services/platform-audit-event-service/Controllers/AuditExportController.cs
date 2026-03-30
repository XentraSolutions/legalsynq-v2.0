using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using PlatformAuditEventService.Authorization;
using PlatformAuditEventService.Configuration;
using PlatformAuditEventService.DTOs.Export;
using PlatformAuditEventService.Services;

namespace PlatformAuditEventService.Controllers;

/// <summary>
/// Export lifecycle endpoints.
///
/// Route prefix: /audit
///
///   POST /audit/exports                 — Submit an async export job.
///   GET  /audit/exports/{exportId}      — Poll the status of an existing job.
///
/// Both endpoints sit under the /audit/* path prefix and are subject to
/// QueryAuthMiddleware scope resolution. Export creation enforces the same
/// multi-tenancy constraints as query endpoints via IQueryAuthorizer.
///
/// Configuration gate:
///   When Export:Provider = "None" (the default), both endpoints return HTTP 503.
///   Set Export:Provider = "Local" (dev) or "S3" / "AzureBlob" (production) to enable.
///
/// Response codes:
///   POST: 202 Accepted  — job created and processed; body = ExportStatusResponse.
///         400 Bad Request — validation failure or unsupported format.
///         401 / 403       — authentication / scope failure.
///         503 Service Unavailable — export not configured.
///
///   GET:  200 OK        — job found; body = ExportStatusResponse.
///         404 Not Found — no job with the given exportId.
///         503 Service Unavailable — export not configured.
/// </summary>
[ApiController]
[Route("audit")]
[Produces("application/json")]
public sealed class AuditExportController : ControllerBase
{
    private readonly IAuditExportService                _exportService;
    private readonly IValidator<ExportRequest>          _validator;
    private readonly ExportOptions                      _exportOpts;
    private readonly ILogger<AuditExportController>     _logger;

    public AuditExportController(
        IAuditExportService             exportService,
        IValidator<ExportRequest>       validator,
        IOptions<ExportOptions>         exportOpts,
        ILogger<AuditExportController>  logger)
    {
        _exportService = exportService;
        _validator     = validator;
        _exportOpts    = exportOpts.Value;
        _logger        = logger;
    }

    // ── POST /audit/exports ───────────────────────────────────────────────────

    /// <summary>
    /// Submit a new audit data export job.
    ///
    /// Accepts filter parameters similar to the query API. Creates a job, processes it
    /// synchronously (v1), and returns the terminal status with the output file reference.
    ///
    /// The caller's authorization scope (resolved by QueryAuthMiddleware) determines
    /// which records are accessible. Cross-tenant access is always denied for
    /// non-PlatformAdmin callers.
    /// </summary>
    [HttpPost("exports")]
    [ProducesResponseType(typeof(ExportStatusResponse), StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Submit(
        [FromBody] ExportRequest request,
        CancellationToken        ct)
    {
        if (!IsExportEnabled(out var disabledResult))
            return disabledResult!;

        // ── Validation ────────────────────────────────────────────────────────
        var validation = await _validator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            return BadRequest(new
            {
                error  = "One or more validation errors occurred.",
                errors = validation.Errors.Select(e => e.ErrorMessage).ToList(),
            });
        }

        // Instance-level format gate (in addition to static validator list)
        if (!_exportOpts.SupportedFormats.Contains(request.Format, StringComparer.Ordinal))
        {
            return BadRequest(new
            {
                error = $"Format '{request.Format}' is not enabled on this instance. " +
                        $"Supported: {string.Join(", ", _exportOpts.SupportedFormats)}."
            });
        }

        // ── Caller context (set by QueryAuthMiddleware) ────────────────────────
        var caller = HttpContext.Items[QueryCallerContext.ItemKey] as IQueryCallerContext
                     ?? QueryCallerContext.Anonymous();

        // ── Delegate to service ───────────────────────────────────────────────
        try
        {
            var result = await _exportService.SubmitAsync(request, caller, ct);
            return StatusCode(StatusCodes.Status202Accepted, result);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(
                "Export access denied for ExportRequest. Scope={Scope} Reason={Reason}",
                caller.Scope, ex.Message);

            return caller.IsAuthenticated
                ? StatusCode(StatusCodes.Status403Forbidden,  new { error = ex.Message })
                : StatusCode(StatusCodes.Status401Unauthorized, new { error = "Authentication required." });
        }
    }

    // ── GET /audit/exports/{exportId} ─────────────────────────────────────────

    /// <summary>
    /// Poll the status of an existing export job.
    ///
    /// Returns the current state of the job including file path / download URL
    /// when Status = Completed, or ErrorMessage when Status = Failed.
    ///
    /// Polling is idempotent — repeated calls return the same data once the job
    /// reaches a terminal state (Completed, Failed, Cancelled, Expired).
    /// </summary>
    [HttpGet("exports/{exportId:guid}")]
    [ProducesResponseType(typeof(ExportStatusResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> GetStatus(
        Guid              exportId,
        CancellationToken ct)
    {
        if (!IsExportEnabled(out var disabledResult))
            return disabledResult!;

        var result = await _exportService.GetStatusAsync(exportId, ct);

        if (result is null)
        {
            return NotFound(new { error = $"Export job '{exportId}' not found." });
        }

        return Ok(result);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns false and sets <paramref name="result"/> to a 503 response when
    /// Export:Provider = "None". Returns true when the export subsystem is active.
    /// </summary>
    private bool IsExportEnabled(out IActionResult? result)
    {
        if (_exportOpts.Provider.Equals("None", StringComparison.OrdinalIgnoreCase))
        {
            result = StatusCode(StatusCodes.Status503ServiceUnavailable, new
            {
                error = "The export service is not enabled on this instance. " +
                        "Set Export:Provider to 'Local', 'S3', or 'AzureBlob' to activate."
            });
            return false;
        }

        result = null;
        return true;
    }
}
