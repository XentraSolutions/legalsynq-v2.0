using Commerce.Application.Integration.Abstractions;
using Commerce.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Commerce.Api.Controllers.Integration;

/// <summary>
/// TB-INT-01 / TB-INT-02 — internal/admin-safe surface for the
/// Commerce → Tenant Billing entitlement publisher. Not intended for
/// tenant-facing callers; mounted under the same
/// <c>/api/commerce/integration</c> prefix as
/// <see cref="HostIntegrationController"/>.
///
/// <para>Behaviour is deterministic: each endpoint either reports
/// <c>published</c>, <c>skipped</c>, or <c>failed</c> (publish), or a
/// preview / diagnostics payload (TB-INT-02). It never throws to the
/// client and it never mutates Commerce state.</para>
/// </summary>
[ApiController]
[Route("api/commerce/integration/tenant-billing")]
public sealed class TenantBillingPublisherController : ControllerBase
{
    private readonly ITenantBillingEntitlementPublisher _publisher;
    private readonly CommerceDbContext _db;

    public TenantBillingPublisherController(
        ITenantBillingEntitlementPublisher publisher,
        CommerceDbContext db)
    {
        _publisher = publisher;
        _db = db;
    }

    [HttpPost("billing-accounts/{billingAccountId:guid}/publish-entitlement")]
    [ProducesResponseType(typeof(PublishEntitlementResultResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PublishEntitlementResultResponse>> PublishForBillingAccount(
        Guid billingAccountId,
        CancellationToken ct)
    {
        if (billingAccountId == Guid.Empty)
        {
            return BadRequest(new { error = "billingAccountId required" });
        }

        var exists = await _db.BillingAccounts
            .AsNoTracking()
            .AnyAsync(b => b.Id == billingAccountId, ct);
        if (!exists)
        {
            return NotFound(new { resource = "billing-account", id = billingAccountId });
        }

        var result = await _publisher.PublishForBillingAccountAsync(billingAccountId, ct);
        return Ok(PublishEntitlementResultResponse.From(result));
    }

    /// <summary>
    /// TB-INT-02 — Builds the same payload the publisher would send,
    /// but performs no HTTP call and mutates no state. Useful for
    /// validating mapping and tenant resolution before flipping the
    /// publisher on.
    /// </summary>
    [HttpPost("billing-accounts/{billingAccountId:guid}/preview-entitlement")]
    [ProducesResponseType(typeof(PreviewEntitlementResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PreviewEntitlementResult>> PreviewForBillingAccount(
        Guid billingAccountId,
        CancellationToken ct)
    {
        if (billingAccountId == Guid.Empty)
        {
            return BadRequest(new { error = "billingAccountId required" });
        }

        var preview = await _publisher.PreviewForBillingAccountAsync(billingAccountId, ct);
        if (preview is null)
        {
            return NotFound(new { resource = "billing-account", id = billingAccountId });
        }
        return Ok(preview);
    }

    /// <summary>
    /// TB-INT-02 — Non-secret view of publisher configuration and
    /// runtime readiness. Internal token is never returned; only a
    /// presence flag.
    /// </summary>
    [HttpGet("diagnostics")]
    [ProducesResponseType(typeof(TenantBillingDiagnostics), StatusCodes.Status200OK)]
    public async Task<ActionResult<TenantBillingDiagnostics>> Diagnostics(CancellationToken ct)
    {
        var diag = await _publisher.GetDiagnosticsAsync(ct);
        return Ok(diag);
    }
}

/// <summary>
/// Wire-shape for the publisher endpoint response. Mirrors
/// <see cref="PublishEntitlementResult"/> but exposes the outcome enum
/// as a stable lower-case string for log/dashboard consumers.
/// </summary>
public sealed record PublishEntitlementResultResponse(
    string Outcome,
    Guid BillingAccountId,
    Guid? TenantId,
    int? HttpStatus,
    string Reason,
    string? ResponseBodySummary,
    int Attempts)
{
    public static PublishEntitlementResultResponse From(PublishEntitlementResult r)
        => new(
            r.Outcome switch
            {
                PublishEntitlementOutcome.Published => "published",
                PublishEntitlementOutcome.Skipped   => "skipped",
                PublishEntitlementOutcome.Failed    => "failed",
                _                                   => "unknown",
            },
            r.BillingAccountId,
            r.TenantId,
            r.HttpStatus,
            r.Reason,
            r.ResponseBodySummary,
            r.Attempts);
}
