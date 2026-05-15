using System.Net;
using Commerce.Application.Integration.Abstractions;
using FluentAssertions;
using Xunit;

namespace Commerce.Tests.Integration.TenantBilling;

/// <summary>
/// TB-INT-02 — circuit breaker behaviour.
/// </summary>
public class TenantBillingPublisherCircuitBreakerTests
{
    [Fact]
    public async Task Disabled_breaker_never_short_circuits()
    {
        var handler = new FakeHttpMessageHandler(HttpStatusCode.InternalServerError);
        var (pub, http, snaps, _, _, _) = PublisherTestHelpers.Build(
            handler: handler,
            retryAttempts: 0,
            circuitBreakerEnabled: false,
            circuitBreakerFailures: 1);

        var ba = Guid.NewGuid();
        snaps.Map[ba] = PublisherTestHelpers.Snapshot(ba, Guid.NewGuid().ToString());

        for (var i = 0; i < 5; i++)
        {
            var r = await pub.PublishForBillingAccountAsync(ba, default);
            r.Outcome.Should().Be(PublishEntitlementOutcome.Failed);
            r.Reason.Should().Be("tenant-billing-500");
        }
        http.CallCount.Should().Be(5);
    }

    [Fact]
    public async Task Opens_after_configured_failures_and_short_circuits_with_circuit_open_reason()
    {
        var handler = new FakeHttpMessageHandler(HttpStatusCode.InternalServerError);
        var (pub, http, snaps, breaker, _, _) = PublisherTestHelpers.Build(
            handler: handler,
            retryAttempts: 0,
            circuitBreakerEnabled: true,
            circuitBreakerFailures: 2,
            circuitBreakerDurationSeconds: 60);

        var ba = Guid.NewGuid();
        snaps.Map[ba] = PublisherTestHelpers.Snapshot(ba, Guid.NewGuid().ToString());

        // First failure: still closed.
        var r1 = await pub.PublishForBillingAccountAsync(ba, default);
        r1.Outcome.Should().Be(PublishEntitlementOutcome.Failed);
        r1.Reason.Should().Be("tenant-billing-500");
        breaker.State.Should().Be("Closed");

        // Second failure: opens the breaker.
        var r2 = await pub.PublishForBillingAccountAsync(ba, default);
        r2.Outcome.Should().Be(PublishEntitlementOutcome.Failed);
        breaker.State.Should().Be("Open");

        // Third call: short-circuited.
        var r3 = await pub.PublishForBillingAccountAsync(ba, default);
        r3.Outcome.Should().Be(PublishEntitlementOutcome.Failed);
        r3.Reason.Should().Be("tenant-billing-circuit-open");
        r3.HttpStatus.Should().BeNull();
        // No new HTTP call made for the short-circuited attempt.
        http.CallCount.Should().Be(2);
    }

    [Fact]
    public async Task Reopens_after_failed_probe()
    {
        // We control time with a mutable clock so the cool-down passes
        // deterministically.
        var now = DateTimeOffset.UtcNow;
        var handler = new FakeHttpMessageHandler(HttpStatusCode.InternalServerError);
        var (pub, http, snaps, breaker, _, _) = PublisherTestHelpers.Build(
            handler: handler,
            retryAttempts: 0,
            circuitBreakerEnabled: true,
            circuitBreakerFailures: 1,
            circuitBreakerDurationSeconds: 30,
            clock: () => now);

        var ba = Guid.NewGuid();
        snaps.Map[ba] = PublisherTestHelpers.Snapshot(ba, Guid.NewGuid().ToString());

        // Trip → Open after a single failure.
        await pub.PublishForBillingAccountAsync(ba, default);
        breaker.State.Should().Be("Open");

        // Still inside cool-down → short-circuit.
        var r = await pub.PublishForBillingAccountAsync(ba, default);
        r.Reason.Should().Be("tenant-billing-circuit-open");

        // Advance past cool-down.
        now = now.AddSeconds(31);

        // Probe is allowed but fails → reopens.
        var r2 = await pub.PublishForBillingAccountAsync(ba, default);
        r2.Outcome.Should().Be(PublishEntitlementOutcome.Failed);
        r2.Reason.Should().Be("tenant-billing-500");
        breaker.State.Should().Be("Open");
    }

    [Fact]
    public async Task Probe_success_closes_breaker()
    {
        var now = DateTimeOffset.UtcNow;
        // Fail once to trip, then succeed on the probe.
        var handler = FakeHttpMessageHandler.Sequence(
            HttpStatusCode.InternalServerError,
            new HttpResponseMessage(HttpStatusCode.OK));
        var (pub, _, snaps, breaker, _, _) = PublisherTestHelpers.Build(
            handler: handler,
            retryAttempts: 0,
            circuitBreakerEnabled: true,
            circuitBreakerFailures: 1,
            circuitBreakerDurationSeconds: 30,
            clock: () => now);

        var ba = Guid.NewGuid();
        snaps.Map[ba] = PublisherTestHelpers.Snapshot(ba, Guid.NewGuid().ToString());

        await pub.PublishForBillingAccountAsync(ba, default);
        breaker.State.Should().Be("Open");

        now = now.AddSeconds(31);

        var r = await pub.PublishForBillingAccountAsync(ba, default);
        r.Outcome.Should().Be(PublishEntitlementOutcome.Published);
        breaker.State.Should().Be("Closed");
    }

    [Fact]
    public async Task HalfOpen_probe_with_non_transient_4xx_closes_breaker()
    {
        // Regression: a non-transient probe response (e.g. 401) used to
        // leave the breaker stuck in HalfOpen forever, blocking every
        // subsequent caller. It must close the breaker so the caller's
        // own retry logic / health checks can resume.
        var now = DateTimeOffset.UtcNow;
        var handler = FakeHttpMessageHandler.Sequence(
            HttpStatusCode.InternalServerError, // trip
            new HttpResponseMessage(HttpStatusCode.Unauthorized), // probe
            new HttpResponseMessage(HttpStatusCode.OK)); // post-probe call
        var (pub, _, snaps, breaker, _, _) = PublisherTestHelpers.Build(
            handler: handler,
            retryAttempts: 0,
            circuitBreakerEnabled: true,
            circuitBreakerFailures: 1,
            circuitBreakerDurationSeconds: 30,
            clock: () => now);

        var ba = Guid.NewGuid();
        snaps.Map[ba] = PublisherTestHelpers.Snapshot(ba, Guid.NewGuid().ToString());

        await pub.PublishForBillingAccountAsync(ba, default);
        breaker.State.Should().Be("Open");

        now = now.AddSeconds(31);

        var probe = await pub.PublishForBillingAccountAsync(ba, default);
        probe.Outcome.Should().Be(PublishEntitlementOutcome.Failed);
        probe.Reason.Should().Be("tenant-billing-401-internal-token-rejected");
        breaker.State.Should().Be("Closed");

        // And subsequent calls are not short-circuited.
        var next = await pub.PublishForBillingAccountAsync(ba, default);
        next.Outcome.Should().Be(PublishEntitlementOutcome.Published);
    }

    [Fact]
    public async Task Non_transient_failure_does_not_trip_breaker()
    {
        var handler = new FakeHttpMessageHandler(HttpStatusCode.BadRequest);
        var (pub, _, snaps, breaker, _, _) = PublisherTestHelpers.Build(
            handler: handler,
            retryAttempts: 0,
            circuitBreakerEnabled: true,
            circuitBreakerFailures: 1);

        var ba = Guid.NewGuid();
        snaps.Map[ba] = PublisherTestHelpers.Snapshot(ba, Guid.NewGuid().ToString());

        await pub.PublishForBillingAccountAsync(ba, default);
        await pub.PublishForBillingAccountAsync(ba, default);
        breaker.State.Should().Be("Closed");
    }
}
