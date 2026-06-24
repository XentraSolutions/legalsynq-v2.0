using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Billing.Api.Contracts;
using Billing.Api.Security;
using Billing.Api.Tenancy;
using Billing.Domain.Services;
using Billing.Domain.Statements;
using Billing.Domain.Statements.Delivery;
using Billing.Domain.StatementTemplates;

namespace Billing.Api.Controllers;

/// <summary>
/// STAT-B01 + STAT-B02 — Tenant-scoped customer statement endpoints.
///
/// STAT-B01 routes (preserved unchanged):
/// <list type="bullet">
///   <item><c>GET /api/statements/customers/{customerId}</c> – build JSON statement.</item>
///   <item><c>GET /api/statements/customers/{customerId}/render/html</c> – render HTML.</item>
///   <item><c>GET /api/statements/customers/{customerId}/monthly</c> – monthly JSON shortcut.</item>
/// </list>
///
/// STAT-B02 routes (persisted snapshots):
/// <list type="bullet">
///   <item><c>POST .../generate</c> – build, snapshot, and persist.</item>
///   <item><c>POST .../monthly/generate</c> – monthly variant.</item>
///   <item><c>GET  .../history</c> – per-customer list of persisted snapshots.</item>
///   <item><c>GET  /api/statements/history/{id}</c> – fetch one persisted snapshot.</item>
///   <item><c>GET  /api/statements/history/{id}/render/html</c> – render persisted snapshot.</item>
///   <item><c>POST /api/statements/history/{id}/void</c> – soft-void a snapshot.</item>
/// </list>
///
/// All routes flow through <see cref="TenantResolutionMiddleware"/>, so
/// the controller can rely on <see cref="ITenantContext"/> resolving to a
/// non-empty GUID; missing / invalid headers are short-circuited with
/// HTTP 400 by the middleware before this controller runs.
/// </summary>
[ApiController]
[Route("api/statements")]
public sealed class StatementsController : ControllerBase
{
    private readonly ICustomerStatementService _statements;
    private readonly ICustomerStatementPersistenceService _persistence;
    private readonly IStatementDeliveryService _delivery;
    private readonly ITenantContext _tenant;
    private readonly IOptionsMonitor<StatementRetryOptions> _retryOptions;
    private readonly IProviderHealthMonitor _providerHealth;
    private readonly TimeProvider _time;

    public StatementsController(
        ICustomerStatementService statements,
        ICustomerStatementPersistenceService persistence,
        IStatementDeliveryService delivery,
        ITenantContext tenant,
        IOptionsMonitor<StatementRetryOptions> retryOptions,
        IProviderHealthMonitor providerHealth,
        TimeProvider time)
    {
        _statements = statements;
        _persistence = persistence;
        _delivery = delivery;
        _tenant = tenant;
        _retryOptions = retryOptions;
        _providerHealth = providerHealth;
        _time = time;
    }

    /// <summary>
    /// MS-BILL-INT-003 — Build the live retryability + provider-
    /// health projections off the (post-attempt or current)
    /// snapshot. Pure composition over the bound options + the
    /// in-memory monitor; never throws.
    /// </summary>
    private (StatementRetryabilityResponse Retry, ProviderHealthResponse Health) BuildLiveProjections(
        Billing.Domain.Entities.CustomerStatement snapshot)
    {
        var opts = _retryOptions.CurrentValue;
        var nowUtc = _time.GetUtcNow().UtcDateTime;
        var decision = StatementRetryability.Evaluate(snapshot, opts, nowUtc);
        var health = _providerHealth.GetHealth(nowUtc);
        return (
            new StatementRetryabilityResponse(
                IsRetryable: decision.IsRetryable,
                Reason: decision.Reason,
                CooldownUntilUtc: decision.CooldownUntilUtc,
                RetriesRemaining: decision.RetriesRemaining,
                MaxAttempts: opts.MaxAttempts > 0 ? opts.MaxAttempts : 1),
            new ProviderHealthResponse(
                State: health.State,
                RecentFailures: health.RecentFailures,
                RecentSuccesses: health.RecentSuccesses,
                WindowSeconds: health.WindowSeconds,
                ObservedAtUtc: health.ObservedAtUtc));
    }

    // ===============================================================
    // STAT-B01 — preserved verbatim
    // ===============================================================

    [HttpGet("customers/{customerId:guid}")]
    [ProducesResponseType(typeof(CustomerStatementDocument), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetJson(
        [FromRoute] Guid customerId,
        [FromQuery] StatementPeriodQuery query,
        CancellationToken ct)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        return await BuildJsonAsync(customerId, query.From!.Value, query.To!.Value, ct);
    }

    [HttpGet("customers/{customerId:guid}/render/html")]
    [Produces("text/html")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetHtml(
        [FromRoute] Guid customerId,
        [FromQuery] StatementPeriodQuery query,
        CancellationToken ct)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        try
        {
            var html = await _statements.RenderHtmlAsync(
                _tenant.TenantId, customerId, query.From!.Value, query.To!.Value, ct);
            if (html is null)
            {
                return Problem(
                    detail: $"Customer '{customerId}' was not found.",
                    statusCode: StatusCodes.Status404NotFound);
            }
            return Content(html, "text/html");
        }
        catch (StatementValidationException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }
        catch (ArgumentException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }
    }

    [HttpGet("customers/{customerId:guid}/monthly")]
    [ProducesResponseType(typeof(CustomerStatementDocument), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMonthlyJson(
        [FromRoute] Guid customerId,
        [FromQuery] StatementMonthlyQuery query,
        CancellationToken ct)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);

        var year = query.Year!.Value;
        var month = query.Month!.Value;
        var from = new DateTime(year, month, 1);
        var to = from.AddMonths(1).AddDays(-1);

        return await BuildJsonAsync(customerId, from, to, ct);
    }

    private async Task<IActionResult> BuildJsonAsync(
        Guid customerId, DateTime from, DateTime to, CancellationToken ct)
    {
        try
        {
            var doc = await _statements.BuildStatementAsync(
                _tenant.TenantId, customerId, from, to, ct);
            if (doc is null)
            {
                return Problem(
                    detail: $"Customer '{customerId}' was not found.",
                    statusCode: StatusCodes.Status404NotFound);
            }
            return Ok(doc);
        }
        catch (StatementValidationException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }
        catch (ArgumentException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }
    }

    // ===============================================================
    // STAT-B02 — persisted snapshots
    // ===============================================================

    [HttpPost("customers/{customerId:guid}/generate")]
    [RequireTenantBillingAccess(TenantBillingOperationCategory.StatementGenerate)]
    [ProducesResponseType(typeof(CustomerStatementResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Generate(
        [FromRoute] Guid customerId,
        [FromBody] GenerateStatementRequest request,
        CancellationToken ct)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        return await PersistResponseAsync(() => _persistence.GenerateAsync(
            _tenant.TenantId, customerId,
            request.PeriodStart!.Value, request.PeriodEnd!.Value,
            request.TemplateId, request.RenderHtml, ct), customerId);
    }

    [HttpPost("customers/{customerId:guid}/monthly/generate")]
    [RequireTenantBillingAccess(TenantBillingOperationCategory.StatementGenerate)]
    [ProducesResponseType(typeof(CustomerStatementResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> GenerateMonthly(
        [FromRoute] Guid customerId,
        [FromBody] GenerateMonthlyStatementRequest request,
        CancellationToken ct)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        return await PersistResponseAsync(() => _persistence.GenerateMonthlyAsync(
            _tenant.TenantId, customerId,
            request.Year!.Value, request.Month!.Value,
            request.TemplateId, request.RenderHtml, ct), customerId);
    }

    [HttpGet("customers/{customerId:guid}/history")]
    [ProducesResponseType(typeof(IReadOnlyList<CustomerStatementSummaryResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ListHistory([FromRoute] Guid customerId, CancellationToken ct)
    {
        try
        {
            var items = await _persistence.ListHistoryAsync(_tenant.TenantId, customerId, ct);
            return Ok(items.Select(CustomerStatementSummaryResponse.From).ToList());
        }
        catch (ArgumentException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }
    }

    [HttpGet("history/{id:guid}")]
    [ProducesResponseType(typeof(CustomerStatementResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetHistory([FromRoute] Guid id, CancellationToken ct)
    {
        try
        {
            var s = await _persistence.GetHistoryAsync(_tenant.TenantId, id, ct);
            if (s is null) return NotFound();
            // MS-BILL-INT-003 — Enrich the detail GET with live
            // retryability + provider-health projections so the
            // snapshot detail page renders the cooldown countdown,
            // retry-limit banner, and provider-health pill on
            // first paint without an extra round-trip.
            var (retry, health) = BuildLiveProjections(s);
            var body = CustomerStatementResponse.From(s) with
            {
                Delivery = StatementDeliveryStateResponse.From(s, retry, health),
            };
            return Ok(body);
        }
        catch (ArgumentException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }
    }

    [HttpGet("history/{id:guid}/render/html")]
    [Produces("text/html")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RenderHistoryHtml([FromRoute] Guid id, CancellationToken ct)
    {
        try
        {
            var html = await _persistence.RenderHtmlAsync(_tenant.TenantId, id, ct);
            return html is null ? NotFound() : Content(html, "text/html");
        }
        catch (StatementValidationException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }
        catch (ArgumentException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }
    }

    // ===============================================================
    // MS-BILL-WRITE-009 — delivery surface for persisted snapshots
    // ===============================================================

    /// <summary>
    /// MS-BILL-WRITE-009 — download a persisted statement snapshot as
    /// HTML. Returns the immutable rendered HTML (from
    /// <see cref="CustomerStatement.HtmlSnapshot"/> when present, or
    /// regenerated from <see cref="CustomerStatement.StatementSnapshotJson"/>
    /// via <c>CustomerStatementHtmlRenderer</c> when absent — the
    /// renderer is pure and consumes ONLY the immutable snapshot JSON,
    /// so the result is byte-stable across calls and never reflects
    /// post-snapshot invoices/payments/adjustments).
    ///
    /// HTML (not PDF) is the product-acceptable download format for
    /// this ticket: the codebase has no PDF engine on the .NET path
    /// (no DinkToPdf, no wkhtmltox), and the prompt explicitly
    /// forbids inventing one. PDF is documented as a follow-up.
    ///
    /// Tenant-scoped via the existing
    /// <see cref="TenantResolutionMiddleware"/>; cross-tenant ids
    /// surface as 404 from <c>_persistence.GetHistoryAsync</c>, never
    /// 403 (no enumeration). The filename is derived from
    /// <see cref="CustomerStatement.StatementNumber"/> through a
    /// strict allowlist sanitiser so a hostile statement number can
    /// never inject CR/LF or quote characters into the
    /// <c>Content-Disposition</c> header.
    /// </summary>
    [HttpGet("history/{id:guid}/download")]
    [Produces("text/html")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> DownloadHistoryHtml(
        [FromRoute] Guid id,
        CancellationToken ct)
    {
        try
        {
            // Two-step lookup so we have the StatementNumber for the
            // filename. Both calls are tenant-scoped; either returning
            // null means "not in this tenant's scope" → 404.
            var snapshot = await _persistence.GetHistoryAsync(_tenant.TenantId, id, ct);
            if (snapshot is null) return NotFound();

            var html = await _persistence.RenderHtmlAsync(_tenant.TenantId, id, ct);
            if (html is null) return NotFound();

            var safeName = BuildSafeStatementFilename(snapshot.StatementNumber, snapshot.Id);
            Response.Headers["Content-Disposition"] =
                $"attachment; filename=\"{safeName}.html\"";
            return Content(html, "text/html");
        }
        catch (StatementValidationException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }
        catch (ArgumentException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }
    }

    /// <summary>
    /// MS-BILL-INT-001 (supersedes MS-BILL-WRITE-009 placeholder) —
    /// Replaces the WRITE-009 hard-coded 503
    /// placeholder. Delegates to <see cref="IStatementDeliveryService"/>,
    /// which loads the snapshot tenant-scoped, resolves the
    /// recipient, invokes the configured
    /// <see cref="IStatementDeliveryProvider"/>, and persists the
    /// deterministic outcome on the snapshot row.
    ///
    /// Status mapping (deterministic, never prose-parsed):
    /// <list type="bullet">
    ///   <item><c>Sent</c> → 200</item>
    ///   <item><c>InvalidRecipient</c> → 422 (the customer record
    ///   needs operator action; retrying without a fix won't help)</item>
    ///   <item><c>ProviderUnavailable</c> → 503 (default NoOp branch
    ///   surfaces here — preserves the WRITE-009 contract bit-for-bit
    ///   so the existing tenant-UI banner still fires)</item>
    ///   <item><c>RetryableFailure</c> → 503</item>
    ///   <item><c>Failed</c> → 502</item>
    /// </list>
    /// In every case the body is the same shape:
    /// <c>{ deliveryStatus, providerConfigured, reason, sentAt,
    /// recipientEmail, provider, deliveryId, statement }</c>, where
    /// <c>statement</c> is the post-attempt
    /// <see cref="CustomerStatementResponse"/> so the UI can re-render
    /// without a follow-up GET.
    ///
    /// The optional <c>X-Sent-By</c> header lets the BFF pass the
    /// IDM-resolved tenant-admin user id for audit; the controller
    /// never trusts a browser-supplied header value beyond storing
    /// it on the audit row.
    /// </summary>
    [HttpPost("history/{id:guid}/send")]
    [RequireTenantBillingAccess(TenantBillingOperationCategory.StatementGenerate)]
    [ProducesResponseType(typeof(StatementSendResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(StatementSendResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(StatementSendResponse), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(StatementSendResponse), StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(typeof(StatementSendResponse), StatusCodes.Status502BadGateway)]
    [ProducesResponseType(typeof(StatementSendResponse), StatusCodes.Status503ServiceUnavailable)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SendHistory(
        [FromRoute] Guid id,
        [FromHeader(Name = "X-Sent-By")] string? sentBy,
        CancellationToken ct)
    {
        try
        {
            var outcome = await _delivery.SendAsync(_tenant.TenantId, id, sentBy, ct);
            if (outcome is null) return NotFound();

            var (retry, health) = BuildLiveProjections(outcome.Snapshot);

            // MS-BILL-INT-003 — Governance short-circuit branch.
            // The orchestrator returned the un-mutated snapshot +
            // a typed RetryDecision; the controller maps the
            // decision reason to a deterministic HTTP code with
            // the same response shape so the UI parses one schema
            // for every outcome.
            if (outcome.Rejection is { } rejection)
            {
                var rejectedBody = StatementSendResponse.From(
                    outcome.Snapshot,
                    retryability: retry,
                    providerHealth: health,
                    overrideStatus: StatementDeliveryStatus.RetryNotAllowed,
                    overrideReason: rejection.Reason);

                if (rejection.Reason == StatementRetryability.Reason.CooldownActive
                    && rejection.CooldownUntilUtc.HasValue)
                {
                    var nowUtc = _time.GetUtcNow().UtcDateTime;
                    var seconds = (int)Math.Max(
                        1,
                        Math.Ceiling((rejection.CooldownUntilUtc.Value - nowUtc).TotalSeconds));
                    Response.Headers["Retry-After"] = seconds.ToString();
                    return StatusCode(StatusCodes.Status429TooManyRequests, rejectedBody);
                }

                // RetryLimitReached + NonRetryableTerminal both map
                // to 409 Conflict — the request is well-formed and
                // authorised, but the snapshot is in a state that
                // refuses further sends. Operator action (raise the
                // cap or fix the recipient) is required.
                return Conflict(rejectedBody);
            }

            var snapshot = outcome.Snapshot;
            var body = StatementSendResponse.From(
                snapshot,
                retryability: retry,
                providerHealth: health);

            return snapshot.DeliveryStatus switch
            {
                StatementDeliveryStatus.Sent
                    => Ok(body),
                StatementDeliveryStatus.InvalidRecipient
                    => StatusCode(StatusCodes.Status422UnprocessableEntity, body),
                StatementDeliveryStatus.ProviderUnavailable
                    => StatusCode(StatusCodes.Status503ServiceUnavailable, body),
                StatementDeliveryStatus.RetryableFailure
                    => StatusCode(StatusCodes.Status503ServiceUnavailable, body),
                StatementDeliveryStatus.Failed
                    => StatusCode(StatusCodes.Status502BadGateway, body),
                // Defensive: an unknown status should never escape
                // the orchestrator (it coerces to Failed), but if it
                // does, surface 502 rather than masquerade as 200.
                _ => StatusCode(StatusCodes.Status502BadGateway, body),
            };
        }
        catch (ArgumentException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }
    }

    /// <summary>
    /// Strict allowlist sanitiser for the <c>Content-Disposition</c>
    /// filename. ASCII letters, digits, dot, dash, and underscore
    /// only; everything else collapses to <c>_</c>. Falls back to
    /// <c>statement-{id}</c> when the source is empty/all-stripped.
    /// Caps length at 80 chars to keep the header bounded. The
    /// sanitiser is intentionally conservative — it never preserves
    /// CR/LF, quote, semicolon, or path separators, so a hostile
    /// snapshot number cannot smuggle a header injection or a path
    /// component into the response.
    /// </summary>
    private static string BuildSafeStatementFilename(string? statementNumber, Guid id)
    {
        var src = statementNumber ?? string.Empty;
        var sb = new System.Text.StringBuilder(src.Length);
        foreach (var ch in src)
        {
            if ((ch >= 'A' && ch <= 'Z') ||
                (ch >= 'a' && ch <= 'z') ||
                (ch >= '0' && ch <= '9') ||
                ch == '-' || ch == '_' || ch == '.')
            {
                sb.Append(ch);
            }
            else
            {
                sb.Append('_');
            }
        }
        var cleaned = sb.ToString().Trim('.', '_', '-');
        if (cleaned.Length == 0) return $"statement-{id:N}";
        return cleaned.Length > 80 ? cleaned.Substring(0, 80) : cleaned;
    }

    [HttpPost("history/{id:guid}/void")]
    [RequireTenantBillingAccess(TenantBillingOperationCategory.StatementGenerate)]
    [ProducesResponseType(typeof(CustomerStatementResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Void(
        [FromRoute] Guid id,
        [FromBody] VoidStatementRequest? request,
        CancellationToken ct)
    {
        try
        {
            var s = await _persistence.VoidAsync(_tenant.TenantId, id, request?.Reason, ct);
            return s is null ? NotFound() : Ok(CustomerStatementResponse.From(s));
        }
        catch (ArgumentException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }
    }

    /// <summary>
    /// Common error mapping for the persistence-write endpoints. The
    /// service-layer exception → status code chart:
    /// <list type="bullet">
    ///   <item><see cref="StatementValidationException"/> → 400</item>
    ///   <item><see cref="StatementTemplateNotFoundInScopeException"/> → 400</item>
    ///   <item><see cref="StatementTemplateNotSelectableException"/> → 400</item>
    ///   <item><see cref="ArgumentException"/> → 400</item>
    ///   <item><see cref="CustomerStatementNumberConflictException"/> → 409</item>
    /// </list>
    /// A null result is mapped to 404 (customer missing in scope).
    /// </summary>
    private async Task<IActionResult> PersistResponseAsync(
        Func<Task<Domain.Entities.CustomerStatement?>> action, Guid customerId)
    {
        try
        {
            var s = await action();
            if (s is null)
            {
                return Problem(
                    detail: $"Customer '{customerId}' was not found.",
                    statusCode: StatusCodes.Status404NotFound);
            }
            return CreatedAtAction(nameof(GetHistory), new { id = s.Id },
                CustomerStatementResponse.From(s));
        }
        catch (StatementValidationException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }
        catch (StatementTemplateNotFoundInScopeException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }
        catch (StatementTemplateNotSelectableException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }
        catch (CustomerStatementNumberConflictException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status409Conflict);
        }
        catch (ArgumentException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }
    }
}
