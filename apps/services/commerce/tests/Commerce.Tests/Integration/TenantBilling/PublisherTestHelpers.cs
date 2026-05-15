using Commerce.Application.Integration.Abstractions;
using Commerce.Contracts.Integration;
using Commerce.Infrastructure.Integration.TenantBilling;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Commerce.Tests.Integration.TenantBilling;

/// <summary>
/// Shared test plumbing for TB-INT-01 + TB-INT-02 publisher tests.
/// </summary>
internal static class PublisherTestHelpers
{
    public static (TenantBillingEntitlementPublisher pub,
                   FakeHttpMessageHandler http,
                   FakeSnapshots snaps,
                   TenantBillingPublisherCircuitBreaker breaker,
                   TenantBillingPublisherMetrics metrics,
                   TenantBillingClientOptions opts)
        Build(
            FakeHttpMessageHandler? handler = null,
            bool enabled = true,
            string baseUrl = "http://tenant-billing.test",
            string token = "tok",
            int retryAttempts = 0,
            int retryDelayMs = 0,
            bool circuitBreakerEnabled = false,
            int circuitBreakerFailures = 5,
            int circuitBreakerDurationSeconds = 30,
            int timeoutSeconds = 5,
            Func<DateTimeOffset>? clock = null)
    {
        handler ??= new FakeHttpMessageHandler(System.Net.HttpStatusCode.OK, "{}");
        var http = new HttpClient(handler);
        var raw = new TenantBillingClientOptions
        {
            Enabled = enabled,
            BaseUrl = baseUrl,
            InternalToken = token,
            TimeoutSeconds = timeoutSeconds,
            RetryAttempts = retryAttempts,
            RetryDelayMilliseconds = retryDelayMs,
            CircuitBreakerEnabled = circuitBreakerEnabled,
            CircuitBreakerFailures = circuitBreakerFailures,
            CircuitBreakerDurationSeconds = circuitBreakerDurationSeconds,
        };
        var monitor = new StaticOptionsMonitor<TenantBillingClientOptions>(raw);
        var breaker = new TenantBillingPublisherCircuitBreaker(monitor, clock ?? (() => DateTimeOffset.UtcNow));
        var metrics = new TenantBillingPublisherMetrics();
        var snaps = new FakeSnapshots();
        var pub = new TenantBillingEntitlementPublisher(
            http, snaps, Options.Create(raw), breaker, metrics,
            NullLogger<TenantBillingEntitlementPublisher>.Instance);
        return (pub, handler, snaps, breaker, metrics, raw);
    }

    public static CommerceEntitlementSnapshot Snapshot(
        Guid? ba = null,
        string? externalTenantId = null,
        string standing = "Good",
        AccessRecommendation rec = AccessRecommendation.Allow)
        => new(
            BillingAccountId: ba ?? Guid.NewGuid(),
            AccountNumber: "ACC",
            DisplayName: "x",
            HostPlatformKey: "host",
            ExternalTenantId: externalTenantId,
            AccountStandingStatus: standing,
            AccountStandingReason: null,
            AccountStandingGracePeriodEndsAtUtc: null,
            AccessRecommendation: rec,
            Products: Array.Empty<EntitlementProductRef>(),
            Plans: Array.Empty<EntitlementPlanRef>(),
            Subscriptions: Array.Empty<EntitlementSubscriptionRef>(),
            Limits: Array.Empty<EntitlementFeatureLimit>(),
            GeneratedAtUtc: DateTime.UtcNow);
}

/// <summary>Tiny in-memory <see cref="ICommerceEntitlementSnapshotService"/>.</summary>
internal sealed class FakeSnapshots : ICommerceEntitlementSnapshotService
{
    public Dictionary<Guid, CommerceEntitlementSnapshot> Map { get; } = new();
    public Task<CommerceEntitlementSnapshot?> GetByBillingAccountAsync(
        Guid billingAccountId, bool includeAllSubscriptionStatuses, CancellationToken ct)
        => Task.FromResult(Map.TryGetValue(billingAccountId, out var s) ? s : null);
    public Task<CommerceEntitlementSnapshot?> GetByHostTenantAsync(
        string hostPlatformKey, string externalTenantId,
        bool includeAllSubscriptionStatuses, CancellationToken ct)
        => Task.FromResult<CommerceEntitlementSnapshot?>(null);
}

/// <summary>
/// Minimal IOptionsMonitor for tests; ignores change notifications.
/// </summary>
internal sealed class StaticOptionsMonitor<T> : IOptionsMonitor<T>
{
    public StaticOptionsMonitor(T value) { CurrentValue = value; }
    public T CurrentValue { get; }
    public T Get(string? name) => CurrentValue;
    public IDisposable? OnChange(Action<T, string?> listener) => null;
}
