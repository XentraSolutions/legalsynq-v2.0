using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Billing.Api.Contracts;
using Billing.Api.Tenancy;
using Billing.Domain.Statements.Analytics;
using Billing.Domain.Statements.Delivery;

namespace Billing.Api.Controllers;

/// <summary>
/// MS-BILL-OPS-002 — Read-only operational analytics over the
/// existing statement-delivery surface. All four routes are
/// gated tenant-admin-only at the BFF (`requireAdminSession`);
/// Billing.Api itself trusts the BFF-injected
/// <c>X-Tenant-Id</c> header (read via
/// <see cref="ITenantContext"/>) and applies it on every query.
///
/// <para>
/// NO route on this controller mutates state, enqueues work,
/// triggers a delivery, or exposes recipient PII / provider
/// secrets. Every aggregate is tenant-scoped at the SQL level
/// (no in-memory cross-tenant filtering).
/// </para>
///
/// <para>
/// The "live" provider-health block reads the process-local
/// <see cref="IProviderHealthMonitor"/> — operator visibility
/// only, never gates a click. Its multi-instance behaviour is
/// documented in §11.16 of the runtime runbook.
/// </para>
/// </summary>
[ApiController]
[Route("api/analytics/delivery")]
public sealed class DeliveryAnalyticsController : ControllerBase
{
    // Lookback windows are clamped to a small operator-meaningful
    // set. We do NOT accept arbitrary day counts — analytics is a
    // dashboard, not a query engine.
    private const int MinWindowDays = 1;
    private const int MaxWindowDays = 90;
    private const int DefaultWindowDays = 7;

    private readonly IBillingDeliveryAnalyticsRepository _analytics;
    private readonly IProviderHealthMonitor _health;
    private readonly IOptionsMonitor<StatementRetryOptions> _retryOptions;
    private readonly TimeProvider _clock;
    private readonly ITenantContext _tenant;
    private readonly ILogger<DeliveryAnalyticsController> _logger;

    public DeliveryAnalyticsController(
        IBillingDeliveryAnalyticsRepository analytics,
        IProviderHealthMonitor health,
        IOptionsMonitor<StatementRetryOptions> retryOptions,
        TimeProvider clock,
        ITenantContext tenant,
        ILogger<DeliveryAnalyticsController> logger)
    {
        _analytics = analytics;
        _health = health;
        _retryOptions = retryOptions;
        _clock = clock;
        _tenant = tenant;
        _logger = logger;
    }

    private static int ClampWindow(int? days)
    {
        var d = days is int v && v > 0 ? v : DefaultWindowDays;
        if (d < MinWindowDays) d = MinWindowDays;
        if (d > MaxWindowDays) d = MaxWindowDays;
        return d;
    }

    private (DateTime FromUtc, DateTime ToUtc, int Days) WindowFrom(int? days)
    {
        var d = ClampWindow(days);
        var nowUtc = _clock.GetUtcNow().UtcDateTime;
        var fromUtc = nowUtc.AddDays(-d);
        return (fromUtc, nowUtc, d);
    }

    [HttpGet("summary")]
    [ProducesResponseType(typeof(DeliverySummaryResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSummary([FromQuery] int? windowDays, CancellationToken ct)
    {
        var (fromUtc, toUtc, days) = WindowFrom(windowDays);
        var row = await _analytics.GetSummaryAsync(_tenant.TenantId, fromUtc, toUtc, ct);
        _logger.LogInformation(
            "billing_analytics.delivery_summary tenant={TenantId} windowDays={WindowDays} attempted={Attempted} sent={Sent}",
            _tenant.TenantId, days, row.EverAttempted, row.Sent);
        return Ok(DeliverySummaryResponse.From(row));
    }

    [HttpGet("trends")]
    [ProducesResponseType(typeof(DeliveryTrendResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTrends([FromQuery] int? windowDays, CancellationToken ct)
    {
        var (fromUtc, toUtc, days) = WindowFrom(windowDays);
        // The trend bucket cap is the same as the window so a 30d
        // request never returns more than 30 daily buckets — bounds
        // the response payload regardless of tenant activity.
        var rows = await _analytics.GetTrendAsync(_tenant.TenantId, fromUtc, toUtc, days, ct);
        _logger.LogInformation(
            "billing_analytics.delivery_trends tenant={TenantId} windowDays={WindowDays} buckets={Buckets}",
            _tenant.TenantId, days, rows.Count);
        return Ok(new DeliveryTrendResponse(
            WindowStartUtc: fromUtc,
            WindowEndUtc: toUtc,
            WindowDays: days,
            Buckets: rows.Select(DeliveryTrendBucketResponse.From).ToList()));
    }

    [HttpGet("retries")]
    [ProducesResponseType(typeof(RetryAnalyticsResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetRetries([FromQuery] int? topN, CancellationToken ct)
    {
        var opts = _retryOptions.CurrentValue;
        var nowUtc = _clock.GetUtcNow().UtcDateTime;
        // Defense-in-depth clamp: also enforced inside the
        // repository, but explicit here so the contract is visible
        // at the API surface (default 10, hard cap 50).
        var safeTopN = topN is int t && t > 0 ? Math.Min(t, 50) : 10;
        var row = await _analytics.GetRetryAnalyticsAsync(
            _tenant.TenantId,
            opts.MaxAttempts,
            opts.CooldownSeconds,
            nowUtc,
            safeTopN,
            ct);
        _logger.LogInformation(
            "billing_analytics.delivery_retries tenant={TenantId} atLimit={AtLimit} inCooldown={InCooldown} anyRetry={AnyRetry}",
            _tenant.TenantId, row.SnapshotsAtRetryLimit, row.SnapshotsInCooldownNow, row.SnapshotsWithAnyRetry);
        return Ok(RetryAnalyticsResponse.From(row));
    }

    [HttpGet("provider-health")]
    [ProducesResponseType(typeof(ProviderHealthAnalyticsResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetProviderHealth(CancellationToken ct)
    {
        var nowUtc = _clock.GetUtcNow().UtcDateTime;
        var live = _health.GetHealth(nowUtc);
        var lifetime = await _analytics.GetProviderLifetimeAsync(_tenant.TenantId, ct);

        // The "active provider name" is the live monitor's view —
        // INT-002 sets it to the real provider name when one is
        // bound, and to the NoOp provider's name otherwise. We
        // mark every lifetime row as IsActive iff its name matches
        // the live snapshot (case-insensitive) so the UI can pin
        // the active provider to the top.
        var activeProvider = ResolveActiveProviderName(lifetime, live);
        var providers = lifetime
            .Select(l => new ProviderLifetimeResponse(
                ProviderName: l.ProviderName,
                IsActive: string.Equals(l.ProviderName, activeProvider, StringComparison.OrdinalIgnoreCase),
                LifetimeSends: l.LifetimeSends,
                LifetimeFailures: l.LifetimeFailures,
                LifetimeRetryableFailures: l.LifetimeRetryableFailures,
                LastSuccessfulSendUtc: l.LastSuccessfulSendUtc,
                LastFailureUtc: l.LastFailureUtc))
            .OrderByDescending(p => p.IsActive)
            .ThenByDescending(p => p.LifetimeSends)
            .ToList();

        _logger.LogInformation(
            "billing_analytics.provider_health tenant={TenantId} state={State} providers={Count}",
            _tenant.TenantId, live.State, providers.Count);

        return Ok(new ProviderHealthAnalyticsResponse(
            ActiveProviderName: activeProvider,
            CurrentHealthState: live.State,
            RecentFailures: live.RecentFailures,
            RecentSuccesses: live.RecentSuccesses,
            WindowSeconds: live.WindowSeconds,
            ObservedAtUtc: live.ObservedAtUtc,
            Providers: providers));
    }

    /// <summary>
    /// Pick the active provider name. The in-memory monitor does
    /// not expose its current provider name (it just classifies
    /// outcomes), so the most recent SQL row with at least one
    /// successful send is the operator-meaningful "active"
    /// provider. Falls back to the most-recently-failed provider,
    /// then to the literal string "(unknown)" so the UI never
    /// renders "null".
    /// </summary>
    private static string ResolveActiveProviderName(
        IReadOnlyList<ProviderLifetimeRow> lifetime,
        ProviderHealthSnapshot live)
    {
        if (lifetime.Count == 0) return "(unknown)";
        var bySuccess = lifetime
            .Where(p => p.LastSuccessfulSendUtc != null)
            .OrderByDescending(p => p.LastSuccessfulSendUtc)
            .FirstOrDefault();
        if (bySuccess != null) return bySuccess.ProviderName;
        var byFailure = lifetime
            .Where(p => p.LastFailureUtc != null)
            .OrderByDescending(p => p.LastFailureUtc)
            .FirstOrDefault();
        return byFailure?.ProviderName ?? lifetime[0].ProviderName;
    }
}
