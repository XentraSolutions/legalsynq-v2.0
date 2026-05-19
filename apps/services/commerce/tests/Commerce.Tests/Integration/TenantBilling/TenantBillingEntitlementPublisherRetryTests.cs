using System.Net;
using Commerce.Application.Integration.Abstractions;
using Commerce.Infrastructure.Integration.TenantBilling;
using FluentAssertions;
using Xunit;

namespace Commerce.Tests.Integration.TenantBilling;

/// <summary>
/// TB-INT-02 — retry-loop behaviour. Verifies the publisher retries
/// only the documented transient failures and bounds the total
/// attempts via <see cref="TenantBillingClientOptions.RetryAttempts"/>.
/// </summary>
public class TenantBillingEntitlementPublisherRetryTests
{
    [Fact]
    public async Task Retries_500_then_succeeds_on_second_attempt()
    {
        var handler = FakeHttpMessageHandler.Sequence(
            HttpStatusCode.InternalServerError,
            new HttpResponseMessage(HttpStatusCode.OK));
        var (pub, http, snaps, _, _, _) =
            PublisherTestHelpers.Build(handler: handler, retryAttempts: 2);

        var ba = Guid.CreateVersion7();
        snaps.Map[ba] = PublisherTestHelpers.Snapshot(ba, Guid.CreateVersion7().ToString());

        var r = await pub.PublishForBillingAccountAsync(ba, default);

        r.Outcome.Should().Be(PublishEntitlementOutcome.Published);
        r.HttpStatus.Should().Be(200);
        r.Attempts.Should().Be(2);
        http.CallCount.Should().Be(2);
    }

    [Fact]
    public async Task Retries_503_three_times_total_and_fails_after_exhaustion()
    {
        var handler = new FakeHttpMessageHandler(HttpStatusCode.ServiceUnavailable);
        var (pub, http, snaps, _, _, _) =
            PublisherTestHelpers.Build(handler: handler, retryAttempts: 2);

        var ba = Guid.CreateVersion7();
        snaps.Map[ba] = PublisherTestHelpers.Snapshot(ba, Guid.CreateVersion7().ToString());

        var r = await pub.PublishForBillingAccountAsync(ba, default);

        r.Outcome.Should().Be(PublishEntitlementOutcome.Failed);
        r.Reason.Should().Be("tenant-billing-503");
        r.Attempts.Should().Be(3); // 1 initial + 2 retries
        http.CallCount.Should().Be(3);
    }

    [Fact]
    public async Task Retries_429_then_succeeds()
    {
        var handler = FakeHttpMessageHandler.Sequence(
            (HttpStatusCode)429,
            new HttpResponseMessage(HttpStatusCode.OK));
        var (pub, http, snaps, _, _, _) =
            PublisherTestHelpers.Build(handler: handler, retryAttempts: 2);

        var ba = Guid.CreateVersion7();
        snaps.Map[ba] = PublisherTestHelpers.Snapshot(ba, Guid.CreateVersion7().ToString());

        var r = await pub.PublishForBillingAccountAsync(ba, default);

        r.Outcome.Should().Be(PublishEntitlementOutcome.Published);
        http.CallCount.Should().Be(2);
    }

    [Fact]
    public async Task Retries_408_then_succeeds()
    {
        var handler = FakeHttpMessageHandler.Sequence(
            HttpStatusCode.RequestTimeout,
            new HttpResponseMessage(HttpStatusCode.OK));
        var (pub, http, snaps, _, _, _) =
            PublisherTestHelpers.Build(handler: handler, retryAttempts: 2);

        var ba = Guid.CreateVersion7();
        snaps.Map[ba] = PublisherTestHelpers.Snapshot(ba, Guid.CreateVersion7().ToString());

        var r = await pub.PublishForBillingAccountAsync(ba, default);

        r.Outcome.Should().Be(PublishEntitlementOutcome.Published);
        http.CallCount.Should().Be(2);
    }

    [Fact]
    public async Task Retries_HttpRequestException_then_fails_after_exhaustion()
    {
        var handler = FakeHttpMessageHandler.Throws(
            new HttpRequestException("connection refused"));
        var (pub, http, snaps, _, _, _) =
            PublisherTestHelpers.Build(handler: handler, retryAttempts: 2);

        var ba = Guid.CreateVersion7();
        snaps.Map[ba] = PublisherTestHelpers.Snapshot(ba, Guid.CreateVersion7().ToString());

        var r = await pub.PublishForBillingAccountAsync(ba, default);

        r.Outcome.Should().Be(PublishEntitlementOutcome.Failed);
        r.Reason.Should().Be("tenant-billing-unreachable");
        r.Attempts.Should().Be(3);
        http.CallCount.Should().Be(3);
    }

    [Fact]
    public async Task Does_not_retry_400()
    {
        var handler = new FakeHttpMessageHandler(HttpStatusCode.BadRequest, "bad");
        var (pub, http, snaps, _, _, _) =
            PublisherTestHelpers.Build(handler: handler, retryAttempts: 5);

        var ba = Guid.CreateVersion7();
        snaps.Map[ba] = PublisherTestHelpers.Snapshot(ba, Guid.CreateVersion7().ToString());

        var r = await pub.PublishForBillingAccountAsync(ba, default);

        r.Outcome.Should().Be(PublishEntitlementOutcome.Failed);
        r.Reason.Should().Be("tenant-billing-400-bad-request");
        r.Attempts.Should().Be(1);
        http.CallCount.Should().Be(1);
    }

    [Fact]
    public async Task Does_not_retry_401()
    {
        var handler = new FakeHttpMessageHandler(HttpStatusCode.Unauthorized);
        var (pub, http, snaps, _, _, _) =
            PublisherTestHelpers.Build(handler: handler, retryAttempts: 5);
        var ba = Guid.CreateVersion7();
        snaps.Map[ba] = PublisherTestHelpers.Snapshot(ba, Guid.CreateVersion7().ToString());

        var r = await pub.PublishForBillingAccountAsync(ba, default);

        r.Outcome.Should().Be(PublishEntitlementOutcome.Failed);
        r.Reason.Should().Be("tenant-billing-401-internal-token-rejected");
        http.CallCount.Should().Be(1);
    }

    [Fact]
    public async Task Does_not_retry_403()
    {
        var handler = new FakeHttpMessageHandler(HttpStatusCode.Forbidden);
        var (pub, http, snaps, _, _, _) =
            PublisherTestHelpers.Build(handler: handler, retryAttempts: 5);
        var ba = Guid.CreateVersion7();
        snaps.Map[ba] = PublisherTestHelpers.Snapshot(ba, Guid.CreateVersion7().ToString());

        var r = await pub.PublishForBillingAccountAsync(ba, default);

        r.Outcome.Should().Be(PublishEntitlementOutcome.Failed);
        r.HttpStatus.Should().Be(403);
        http.CallCount.Should().Be(1);
    }

    [Fact]
    public async Task Does_not_retry_404()
    {
        var handler = new FakeHttpMessageHandler(HttpStatusCode.NotFound);
        var (pub, http, snaps, _, _, _) =
            PublisherTestHelpers.Build(handler: handler, retryAttempts: 5);
        var ba = Guid.CreateVersion7();
        snaps.Map[ba] = PublisherTestHelpers.Snapshot(ba, Guid.CreateVersion7().ToString());

        var r = await pub.PublishForBillingAccountAsync(ba, default);

        r.Outcome.Should().Be(PublishEntitlementOutcome.Failed);
        r.Reason.Should().Be("tenant-billing-404-no-profile-for-billing-account");
        http.CallCount.Should().Be(1);
    }

    [Fact]
    public async Task Does_not_retry_409()
    {
        var handler = new FakeHttpMessageHandler(HttpStatusCode.Conflict);
        var (pub, http, snaps, _, _, _) =
            PublisherTestHelpers.Build(handler: handler, retryAttempts: 5);
        var ba = Guid.CreateVersion7();
        snaps.Map[ba] = PublisherTestHelpers.Snapshot(ba, Guid.CreateVersion7().ToString());

        var r = await pub.PublishForBillingAccountAsync(ba, default);

        r.Outcome.Should().Be(PublishEntitlementOutcome.Failed);
        r.Reason.Should().Be("tenant-billing-409-profile-mismatch-or-closed");
        http.CallCount.Should().Be(1);
    }

    [Fact]
    public async Task Zero_retries_does_only_one_attempt()
    {
        var handler = new FakeHttpMessageHandler(HttpStatusCode.InternalServerError);
        var (pub, http, snaps, _, _, _) =
            PublisherTestHelpers.Build(handler: handler, retryAttempts: 0);

        var ba = Guid.CreateVersion7();
        snaps.Map[ba] = PublisherTestHelpers.Snapshot(ba, Guid.CreateVersion7().ToString());

        var r = await pub.PublishForBillingAccountAsync(ba, default);

        r.Outcome.Should().Be(PublishEntitlementOutcome.Failed);
        r.Attempts.Should().Be(1);
        http.CallCount.Should().Be(1);
    }

    [Fact]
    public void Retry_attempts_clamped_when_negative()
    {
        var raw = new TenantBillingClientOptions { RetryAttempts = -5 };
        raw.Normalised().RetryAttempts.Should().Be(0);
    }

    [Fact]
    public void Retry_attempts_clamped_when_too_large()
    {
        var raw = new TenantBillingClientOptions { RetryAttempts = 9999 };
        raw.Normalised().RetryAttempts.Should().Be(10);
    }
}
