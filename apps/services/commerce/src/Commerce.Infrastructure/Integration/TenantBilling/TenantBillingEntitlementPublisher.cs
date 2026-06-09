using System.Net;
using System.Net.Http.Json;
using Commerce.Application.Integration.Abstractions;
using Commerce.Contracts.Integration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Commerce.Infrastructure.Integration.TenantBilling;

/// <summary>
/// TB-INT-01 / TB-INT-02 — default
/// <see cref="ITenantBillingEntitlementPublisher"/> implementation.
/// Builds a Commerce entitlement snapshot, resolves the caller's GUID
/// tenant id from the snapshot's
/// <see cref="CommerceEntitlementSnapshot.ExternalTenantId"/>, maps to
/// the Tenant Billing apply payload, and POSTs it.
///
/// <para>Never throws on transport failure: any exception is caught
/// and surfaced as <see cref="PublishEntitlementOutcome.Failed"/> so
/// callers can react deterministically. Commerce state is never
/// mutated by this publisher under any outcome.</para>
///
/// <para>TB-INT-02 adds bounded retry on transient failures
/// (HttpRequestException, timeout, HTTP 408/429/5xx), an opt-in
/// in-process circuit breaker, structured logging on every code path,
/// counters via <see cref="TenantBillingPublisherMetrics"/>, plus
/// <see cref="PreviewForBillingAccountAsync"/> and
/// <see cref="GetDiagnosticsAsync"/> companions.</para>
/// </summary>
public sealed class TenantBillingEntitlementPublisher : ITenantBillingEntitlementPublisher
{
    private const string ApplyPath = "/api/tenant-billing/entitlements/apply";

    private readonly HttpClient _http;
    private readonly ICommerceEntitlementSnapshotService _snapshots;
    private readonly TenantBillingClientOptions _rawOptions;
    private readonly ITenantBillingPublisherCircuitBreaker _breaker;
    private readonly TenantBillingPublisherMetrics _metrics;
    private readonly ILogger<TenantBillingEntitlementPublisher> _log;
    // TB-INT-03 — optional so existing TB-INT-01/02 unit tests that
    // construct the publisher directly without a queue continue to
    // work. In production DI the queue is always registered.
    private readonly ITenantBillingEntitlementPublishQueue? _queue;
    // TB-INT-04 — optional so existing TB-INT-01/02/03 tests that
    // construct the publisher directly continue to compile. In
    // production DI the outbox is always registered.
    private readonly ITenantBillingEntitlementOutbox? _outbox;

    public TenantBillingEntitlementPublisher(
        HttpClient http,
        ICommerceEntitlementSnapshotService snapshots,
        IOptions<TenantBillingClientOptions> options,
        ITenantBillingPublisherCircuitBreaker breaker,
        TenantBillingPublisherMetrics metrics,
        ILogger<TenantBillingEntitlementPublisher> log,
        ITenantBillingEntitlementPublishQueue? queue = null,
        ITenantBillingEntitlementOutbox? outbox = null)
    {
        _http = http;
        _snapshots = snapshots;
        _rawOptions = options.Value;
        _breaker = breaker;
        _metrics = metrics;
        _log = log;
        _queue = queue;
        _outbox = outbox;
    }

    private TenantBillingClientOptions Options => _rawOptions.Normalised();

    // ────────────────────────── Publish ──────────────────────────

    public async Task<PublishEntitlementResult> PublishForBillingAccountAsync(
        Guid billingAccountId, CancellationToken ct)
    {
        var opts = Options;
        if (!opts.Enabled)
        {
            _log.LogInformation(
                "TenantBilling publish skipped: BA {BillingAccountId} reason {Reason}",
                billingAccountId, "publisher-disabled");
            return Track(PublishEntitlementResult.Skipped(
                billingAccountId, "publisher-disabled"));
        }

        var snapshot = await _snapshots.GetByBillingAccountAsync(
            billingAccountId, includeAllSubscriptionStatuses: false, ct);
        if (snapshot is null)
        {
            _log.LogInformation(
                "TenantBilling publish skipped: BA {BillingAccountId} reason {Reason}",
                billingAccountId, "billing-account-not-found");
            return Track(PublishEntitlementResult.Skipped(
                billingAccountId, "billing-account-not-found"));
        }

        if (!TryResolveTenantId(snapshot, out var tenantId, out var resolveReason))
        {
            _log.LogWarning(
                "TenantBilling publish skipped: BA {BillingAccountId} reason {Reason}",
                billingAccountId, resolveReason);
            return Track(PublishEntitlementResult.Skipped(
                billingAccountId, resolveReason));
        }

        return Track(await PublishSnapshotInternalAsync(snapshot, tenantId, opts, ct));
    }

    public Task<PublishEntitlementResult> PublishSnapshotAsync(
        CommerceEntitlementSnapshot snapshot, Guid tenantId, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var opts = Options;

        if (!opts.Enabled)
        {
            return Task.FromResult(Track(PublishEntitlementResult.Skipped(
                snapshot.BillingAccountId, "publisher-disabled", tenantId)));
        }
        if (tenantId == Guid.Empty)
        {
            return Task.FromResult(Track(PublishEntitlementResult.Skipped(
                snapshot.BillingAccountId, "tenant-id-empty")));
        }
        return InvokeAndTrackAsync(snapshot, tenantId, opts, ct);
    }

    private async Task<PublishEntitlementResult> InvokeAndTrackAsync(
        CommerceEntitlementSnapshot snapshot, Guid tenantId,
        TenantBillingClientOptions opts, CancellationToken ct)
        => Track(await PublishSnapshotInternalAsync(snapshot, tenantId, opts, ct));

    private PublishEntitlementResult Track(PublishEntitlementResult result)
    {
        var outcome = result.Outcome switch
        {
            PublishEntitlementOutcome.Published => "published",
            PublishEntitlementOutcome.Skipped   => "skipped",
            PublishEntitlementOutcome.Failed    => "failed",
            _                                   => "unknown",
        };
        _metrics.RecordOutcome(outcome, result.Reason, result.HttpStatus);
        return result;
    }

    private async Task<PublishEntitlementResult> PublishSnapshotInternalAsync(
        CommerceEntitlementSnapshot snapshot, Guid tenantId,
        TenantBillingClientOptions opts, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(opts.BaseUrl))
        {
            _log.LogError(
                "TenantBilling publish misconfigured: BA {BillingAccountId} reason {Reason}",
                snapshot.BillingAccountId, "base-url-not-configured");
            return PublishEntitlementResult.Failed(
                snapshot.BillingAccountId, "base-url-not-configured", tenantId);
        }
        if (string.IsNullOrWhiteSpace(opts.InternalToken))
        {
            _log.LogError(
                "TenantBilling publish misconfigured: BA {BillingAccountId} reason {Reason}",
                snapshot.BillingAccountId, "internal-token-not-configured");
            return PublishEntitlementResult.Failed(
                snapshot.BillingAccountId, "internal-token-not-configured", tenantId);
        }

        // Defence-in-depth catch-all: even URI parse / config / serialiser
        // surprises must surface as a deterministic Failed outcome rather
        // than throw to the caller. Reasons are stable (no exception text)
        // so callers can pattern-match; details go to logs only.
        try
        {
            return await SendApplyWithRetryAsync(snapshot, tenantId, opts, ct);
        }
        catch (Exception ex)
        {
            _log.LogError(ex,
                "TenantBilling publish unexpected exception for BA {BillingAccountId}",
                snapshot.BillingAccountId);
            return PublishEntitlementResult.Failed(
                snapshot.BillingAccountId,
                "tenant-billing-publish-exception",
                tenantId);
        }
    }

    private async Task<PublishEntitlementResult> SendApplyWithRetryAsync(
        CommerceEntitlementSnapshot snapshot, Guid tenantId,
        TenantBillingClientOptions opts, CancellationToken ct)
    {
        // Circuit breaker check up front: do not even build the payload.
        if (!_breaker.TryEnter())
        {
            _log.LogWarning(
                "TenantBilling publish short-circuited: BA {BillingAccountId} reason {Reason}",
                snapshot.BillingAccountId, "tenant-billing-circuit-open");
            return PublishEntitlementResult.Failed(
                snapshot.BillingAccountId,
                "tenant-billing-circuit-open",
                tenantId,
                attempts: 0);
        }

        var totalAttempts = opts.RetryAttempts + 1;
        PublishEntitlementResult lastTransient = PublishEntitlementResult.Failed(
            snapshot.BillingAccountId, "tenant-billing-publish-exception", tenantId);

        for (var attempt = 1; attempt <= totalAttempts; attempt++)
        {
            ct.ThrowIfCancellationRequested();
            _log.LogInformation(
                "TenantBilling publish attempt {AttemptNumber}/{TotalAttempts} BA {BillingAccountId} tenant {TenantId} sourceSubscriptionId {SourceSubscriptionId} sourcePlanKey {SourcePlanKey} sourceProductKey {SourceProductKey}",
                attempt, totalAttempts, snapshot.BillingAccountId, tenantId,
                FirstSubscriptionId(snapshot), FirstPlanKey(snapshot), FirstProductKey(snapshot));

            var (result, isTransient) = await SendOneAsync(
                snapshot, tenantId, opts, attempt, ct);

            if (result.Outcome == PublishEntitlementOutcome.Published)
            {
                _breaker.RecordSuccess();
                if (attempt > 1)
                {
                    _log.LogInformation(
                        "TenantBilling publish succeeded after retry: BA {BillingAccountId} tenant {TenantId} attempts {Attempts}",
                        snapshot.BillingAccountId, tenantId, attempt);
                }
                else
                {
                    _log.LogInformation(
                        "TenantBilling publish succeeded: BA {BillingAccountId} tenant {TenantId} status {HttpStatus}",
                        snapshot.BillingAccountId, tenantId, result.HttpStatus);
                }
                return result with { Attempts = attempt };
            }

            if (!isTransient)
            {
                // Non-retryable client error (400/401/403/404/409). Do
                // not record a transient failure on the breaker; do not
                // retry. Caller-side bug, not a service-health signal.
                // We DO call RecordSuccess so a HalfOpen probe doesn't
                // wedge the breaker open forever: the downstream
                // answered, even if rejecting our request, which means
                // it is reachable. RecordSuccess is a no-op when Closed
                // and resets the consecutive-failure counter — both
                // safe.
                _breaker.RecordSuccess();
                _log.LogWarning(
                    "TenantBilling publish non-retryable failure: BA {BillingAccountId} reason {Reason} status {HttpStatus}",
                    snapshot.BillingAccountId, result.Reason, result.HttpStatus);
                return result with { Attempts = attempt };
            }

            lastTransient = result with { Attempts = attempt };

            // Transient — schedule a retry if we have budget left.
            if (attempt < totalAttempts)
            {
                if (opts.RetryDelayMilliseconds > 0)
                {
                    try
                    {
                        await Task.Delay(opts.RetryDelayMilliseconds, ct);
                    }
                    catch (OperationCanceledException) when (ct.IsCancellationRequested)
                    {
                        throw;
                    }
                }
            }
        }

        _breaker.RecordTransientFailure();
        _log.LogError(
            "TenantBilling publish exhausted retries: BA {BillingAccountId} attempts {Attempts} reason {Reason} status {HttpStatus}",
            snapshot.BillingAccountId, lastTransient.Attempts,
            lastTransient.Reason, lastTransient.HttpStatus);
        return lastTransient;
    }

    private async Task<(PublishEntitlementResult Result, bool IsTransient)> SendOneAsync(
        CommerceEntitlementSnapshot snapshot, Guid tenantId,
        TenantBillingClientOptions opts, int attemptNumber, CancellationToken ct)
    {
        var payload = TenantBillingEntitlementMapper.Map(snapshot);
        var url = CombineUrl(opts.BaseUrl, ApplyPath);

        using var req = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(payload),
        };
        req.Headers.Add("X-Tenant-Id", tenantId.ToString("D"));
        req.Headers.Add("X-Internal-Token", opts.InternalToken);

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(Math.Max(1, opts.TimeoutSeconds)));

        HttpResponseMessage resp;
        try
        {
            resp = await _http.SendAsync(req, cts.Token);
            _metrics.RecordAttempt(attemptNumber, (int)resp.StatusCode);
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            _metrics.RecordAttempt(attemptNumber, httpStatus: 408);
            _log.LogWarning(
                "TenantBilling publish timed out after {Seconds}s for BA {BillingAccountId} attempt {AttemptNumber}",
                opts.TimeoutSeconds, snapshot.BillingAccountId, attemptNumber);
            return (PublishEntitlementResult.Failed(
                snapshot.BillingAccountId, "tenant-billing-timeout", tenantId), true);
        }
        catch (HttpRequestException ex)
        {
            _metrics.RecordAttempt(attemptNumber, httpStatus: null);
            _log.LogWarning(ex,
                "TenantBilling publish transport error for BA {BillingAccountId} attempt {AttemptNumber}: {Message}",
                snapshot.BillingAccountId, attemptNumber, ex.Message);
            return (PublishEntitlementResult.Failed(
                snapshot.BillingAccountId,
                "tenant-billing-unreachable",
                tenantId,
                responseBodySummary: Truncate(ex.Message, 256)), true);
        }

        try
        {
            var status = (int)resp.StatusCode;
            if (resp.IsSuccessStatusCode)
            {
                return (PublishEntitlementResult.Published(
                    snapshot.BillingAccountId, tenantId, status), false);
            }

            var body = await SafeReadBodyAsync(resp);
            var reason = status switch
            {
                401 => "tenant-billing-401-internal-token-rejected",
                404 => "tenant-billing-404-no-profile-for-billing-account",
                409 => "tenant-billing-409-profile-mismatch-or-closed",
                400 => "tenant-billing-400-bad-request",
                _   => $"tenant-billing-{status}",
            };
            var transient = IsTransientStatus(status);
            return (PublishEntitlementResult.Failed(
                snapshot.BillingAccountId, reason, tenantId, status, body), transient);
        }
        finally
        {
            resp.Dispose();
        }
    }

    private static bool IsTransientStatus(int status)
        => status == 408 || status == 429 || status >= 500;

    // ────────────────────────── Preview ──────────────────────────

    public async Task<PreviewEntitlementResult?> PreviewForBillingAccountAsync(
        Guid billingAccountId, CancellationToken ct)
    {
        var snapshot = await _snapshots.GetByBillingAccountAsync(
            billingAccountId, includeAllSubscriptionStatuses: false, ct);
        if (snapshot is null)
        {
            return null;
        }

        var opts = Options;

        if (!TryResolveTenantId(snapshot, out var tenantId, out var resolveReason))
        {
            return new PreviewEntitlementResult(
                billingAccountId,
                TenantId: null,
                CanPublish: false,
                SkipReason: resolveReason,
                TenantBillingPayload: null);
        }

        var dto = TenantBillingEntitlementMapper.Map(snapshot);
        var payload = new TenantBillingPreviewPayload(
            dto.BillingAccountId,
            dto.SourceSystem,
            dto.EntitlementStatus,
            dto.AccessRecommendation,
            dto.SourceSnapshotId,
            dto.SourceSubscriptionId,
            dto.SourcePlanKey,
            dto.SourceProductKey,
            dto.Reason,
            dto.EffectiveFromUtc,
            dto.EffectiveToUtc,
            dto.RawSnapshotJson);

        var canPublish = opts.Enabled
            && !string.IsNullOrWhiteSpace(opts.BaseUrl)
            && !string.IsNullOrWhiteSpace(opts.InternalToken);
        var skipReason = canPublish
            ? null
            : !opts.Enabled
                ? "publisher-disabled"
                : string.IsNullOrWhiteSpace(opts.BaseUrl)
                    ? "base-url-not-configured"
                    : "internal-token-not-configured";

        return new PreviewEntitlementResult(
            billingAccountId,
            tenantId,
            canPublish,
            skipReason,
            payload);
    }

    // ────────────────────────── Diagnostics ──────────────────────────

    public async Task<TenantBillingDiagnostics> GetDiagnosticsAsync(CancellationToken ct)
    {
        var opts = Options;
        var baseUrlConfigured = !string.IsNullOrWhiteSpace(opts.BaseUrl);
        var tokenConfigured = !string.IsNullOrWhiteSpace(opts.InternalToken);
        var mode = !opts.Enabled
            ? "Disabled"
            : (baseUrlConfigured && tokenConfigured) ? "Ready" : "Misconfigured";

        TenantBillingEntitlementOutboxCounts? outboxCounts = null;
        if (_outbox is not null)
        {
            try
            {
                outboxCounts = await _outbox.GetCountsAsync(ct).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex,
                    "Outbox count query failed in diagnostics; reporting zeros.");
            }
        }

        return new TenantBillingDiagnostics(
            Enabled: opts.Enabled,
            BaseUrlConfigured: baseUrlConfigured,
            InternalTokenConfigured: tokenConfigured,
            TimeoutSeconds: opts.TimeoutSeconds,
            RetryAttempts: opts.RetryAttempts,
            RetryDelayMilliseconds: opts.RetryDelayMilliseconds,
            CircuitBreakerEnabled: opts.CircuitBreakerEnabled,
            CircuitBreakerFailures: opts.CircuitBreakerFailures,
            CircuitBreakerDurationSeconds: opts.CircuitBreakerDurationSeconds,
            CircuitBreakerState: _breaker.State,
            TargetRoute: ApplyPath,
            Mode: mode,
            // TB-INT-03 — auto-publish posture. WorkerRegistered is true
            // iff the queue dependency was wired in DI (the worker is
            // registered alongside it).
            AutoPublishEnabled: opts.AutoPublishEnabled,
            AutoPublishQueueCapacity: _queue?.Capacity ?? opts.AutoPublishQueueCapacity,
            AutoPublishQueueDepth: _queue?.Depth ?? 0,
            WorkerRegistered: _queue is not null,
            // TB-INT-04 — durable outbox posture.
            OutboxEnabled: opts.OutboxEnabled,
            OutboxBatchSize: opts.OutboxBatchSize,
            OutboxPollSeconds: opts.OutboxPollSeconds,
            OutboxMaxAttempts: opts.OutboxMaxAttempts,
            OutboxRetryBaseDelaySeconds: opts.OutboxRetryBaseDelaySeconds,
            OutboxPendingCount: outboxCounts?.Pending ?? 0,
            OutboxFailedCount: outboxCounts?.Failed ?? 0,
            OutboxProcessingCount: outboxCounts?.Processing ?? 0,
            OutboxAbandonedCount: outboxCounts?.Abandoned ?? 0,
            OutboxPublishedCount: outboxCounts?.Published ?? 0,
            OutboxWorkerRegistered: _outbox is not null);
    }

    // ────────────────────────── Helpers ──────────────────────────

    private static string? Truncate(string? s, int max)
        => string.IsNullOrEmpty(s) ? s : (s!.Length > max ? s[..max] : s);

    private static string? FirstSubscriptionId(CommerceEntitlementSnapshot s)
        => s.Subscriptions.Count > 0 ? s.Subscriptions[0].SubscriptionId.ToString() : null;

    private static string? FirstPlanKey(CommerceEntitlementSnapshot s)
        => s.Subscriptions.Count > 0 && s.Subscriptions[0].Items.Count > 0
            ? s.Subscriptions[0].Items[0].PlanKey
            : null;

    private static string? FirstProductKey(CommerceEntitlementSnapshot s)
    {
        var planKey = FirstPlanKey(s);
        if (planKey is null) return null;
        return s.Plans.FirstOrDefault(p =>
            string.Equals(p.PlanKey, planKey, StringComparison.Ordinal))?.ProductKey;
    }

    /// <summary>
    /// TB-INT-01 §5 — TenantId resolution. Requires
    /// <c>ExternalTenantId</c> to parse as a non-empty GUID; never
    /// calls Identity, never invents ids, never falls back to
    /// BillingAccountId.
    /// </summary>
    internal static bool TryResolveTenantId(
        CommerceEntitlementSnapshot snapshot,
        out Guid tenantId,
        out string skipReason)
    {
        tenantId = Guid.Empty;
        if (string.IsNullOrWhiteSpace(snapshot.ExternalTenantId))
        {
            skipReason = "no-external-tenant-id";
            return false;
        }
        if (!Guid.TryParse(snapshot.ExternalTenantId, out var parsed) || parsed == Guid.Empty)
        {
            skipReason = "external-tenant-id-not-a-guid";
            return false;
        }
        tenantId = parsed;
        skipReason = string.Empty;
        return true;
    }

    private static string CombineUrl(string baseUrl, string path)
    {
        var trimmed = baseUrl.TrimEnd('/');
        return string.Concat(trimmed, path);
    }

    private static async Task<string?> SafeReadBodyAsync(HttpResponseMessage resp)
    {
        try
        {
            var raw = await resp.Content.ReadAsStringAsync();
            if (string.IsNullOrWhiteSpace(raw)) return null;
            return raw.Length > 1024 ? raw[..1024] : raw;
        }
        catch
        {
            return null;
        }
    }
}
