using Microsoft.AspNetCore.Mvc;
using PlatformAuditEventService.DTOs;
using PlatformAuditEventService.DTOs.LegalHold;
using PlatformAuditEventService.Services;

namespace PlatformAuditEventService.Controllers;

/// <summary>
/// API endpoints for placing and releasing legal holds on audit event records.
///
/// Route prefix: /audit/legal-holds
///
/// Legal holds prevent the retention pipeline from archiving or deleting held records.
/// This controller is intended for use by compliance officers and legal staff.
///
/// Authorization:
///   All endpoints require PlatformAdmin or ComplianceOfficer scope.
///   In Mode=Bearer, these are resolved from JWT claims.
///   In Mode=None (dev), all callers have PlatformAdmin scope by default.
///
/// HIPAA alignment:
///   All hold creation and release operations are logged at WARNING level for
///   compliance audit trail purposes. The log lines include HoldId, AuditId,
///   LegalAuthority, and the identity of the requester.
/// </summary>
[ApiController]
[Route("audit/legal-holds")]
[Produces("application/json")]
public sealed class LegalHoldController : ControllerBase
{
    private readonly ILegalHoldService                _holdService;
    private readonly ILogger<LegalHoldController>     _logger;

    public LegalHoldController(
        ILegalHoldService             holdService,
        ILogger<LegalHoldController>  logger)
    {
        _holdService = holdService;
        _logger      = logger;
    }

    // ── POST /audit/legal-holds/{auditId} ─────────────────────────────────────

    /// <summary>
    /// Place a legal hold on an audit event record.
    ///
    /// The hold prevents the retention pipeline from archiving or deleting the record
    /// until the hold is explicitly released.
    /// </summary>
    /// <param name="auditId">The AuditId of the record to hold.</param>
    /// <param name="request">Legal hold details.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="201">Hold created successfully.</response>
    /// <response code="404">Audit record not found.</response>
    /// <response code="400">Invalid request.</response>
    [HttpPost("{auditId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<LegalHoldResponse>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateHold(
        Guid                    auditId,
        [FromBody] CreateLegalHoldRequest request,
        CancellationToken       ct)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse<object>.Fail("Invalid request"));

        try
        {
            var userId = ResolveCallerId();
            var hold   = await _holdService.CreateHoldAsync(auditId, userId, request, ct);

            return StatusCode(StatusCodes.Status201Created,
                ApiResponse<LegalHoldResponse>.Ok(hold));
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning("LegalHold creation failed: {Reason}", ex.Message);
            return NotFound(ApiResponse<object>.Fail(ex.Message));
        }
    }

    // ── POST /audit/legal-holds/{holdId}/release ──────────────────────────────

    /// <summary>
    /// Release an active legal hold.
    ///
    /// After release, the record becomes eligible for the normal retention lifecycle.
    /// </summary>
    /// <param name="holdId">The HoldId of the hold to release.</param>
    /// <param name="request">Optional release notes.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="200">Hold released successfully.</response>
    /// <response code="404">Hold not found.</response>
    /// <response code="409">Hold is already released.</response>
    [HttpPost("{holdId:guid}/release")]
    [ProducesResponseType(typeof(ApiResponse<LegalHoldResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ReleaseHold(
        Guid holdId,
        [FromBody] ReleaseLegalHoldRequest request,
        CancellationToken ct)
    {
        try
        {
            var userId = ResolveCallerId();
            var hold   = await _holdService.ReleaseHoldAsync(holdId, userId, request, ct);
            return Ok(ApiResponse<LegalHoldResponse>.Ok(hold));
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("already released"))
        {
            return Conflict(ApiResponse<object>.Fail(ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning("LegalHold release failed: {Reason}", ex.Message);
            return NotFound(ApiResponse<object>.Fail(ex.Message));
        }
    }

    // ── GET /audit/legal-holds/record/{auditId} ───────────────────────────────

    /// <summary>
    /// List all holds (active and released) for an audit event record.
    /// </summary>
    /// <param name="auditId">The AuditId of the record.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="200">List of holds for the record (may be empty).</response>
    [HttpGet("record/{auditId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<LegalHoldResponse>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ListByAuditId(Guid auditId, CancellationToken ct)
    {
        var holds = await _holdService.ListByAuditIdAsync(auditId, ct);
        return Ok(ApiResponse<IReadOnlyList<LegalHoldResponse>>.Ok(holds));
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    /// <summary>
    /// Resolves the caller identity. In Bearer mode this is the JWT sub claim.
    /// In None mode (dev), returns a placeholder identity string.
    /// </summary>
    private string ResolveCallerId()
    {
        // Sub claim (OpenID Connect standard for user identity)
        var sub = User.FindFirst("sub")?.Value
               ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

        if (!string.IsNullOrWhiteSpace(sub))
            return sub;

        // Fallback for dev mode (Mode=None — no JWT)
        return "system:dev-caller";
    }
}
