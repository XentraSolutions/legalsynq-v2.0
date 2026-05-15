using System.Net;
using Commerce.Application.Integration.Abstractions;
using FluentAssertions;
using Xunit;

namespace Commerce.Tests.Integration.TenantBilling;

/// <summary>
/// TB-INT-01 — publisher unit tests. We use a fake snapshot service and
/// fake HTTP handler so each case is fully deterministic and never
/// touches the real Tenant Billing service.
/// </summary>
public class TenantBillingEntitlementPublisherTests
{
    [Fact]
    public async Task Disabled_publisher_returns_skipped_and_makes_no_http_call()
    {
        var (pub, http, snaps, _, _, _) = PublisherTestHelpers.Build(enabled: false);
        var ba = Guid.NewGuid();
        snaps.Map[ba] = PublisherTestHelpers.Snapshot(ba, Guid.NewGuid().ToString());

        var r = await pub.PublishForBillingAccountAsync(ba, default);

        r.Outcome.Should().Be(PublishEntitlementOutcome.Skipped);
        r.Reason.Should().Be("publisher-disabled");
        http.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task Unknown_billing_account_returns_skipped()
    {
        var (pub, http, _, _, _, _) = PublisherTestHelpers.Build();
        var r = await pub.PublishForBillingAccountAsync(Guid.NewGuid(), default);
        r.Outcome.Should().Be(PublishEntitlementOutcome.Skipped);
        r.Reason.Should().Be("billing-account-not-found");
        http.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task Missing_external_tenant_id_returns_skipped_and_does_not_call()
    {
        var (pub, http, snaps, _, _, _) = PublisherTestHelpers.Build();
        var ba = Guid.NewGuid();
        snaps.Map[ba] = PublisherTestHelpers.Snapshot(ba, externalTenantId: null);
        var r = await pub.PublishForBillingAccountAsync(ba, default);
        r.Outcome.Should().Be(PublishEntitlementOutcome.Skipped);
        r.Reason.Should().Be("no-external-tenant-id");
        http.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task Non_guid_external_tenant_id_returns_skipped()
    {
        var (pub, http, snaps, _, _, _) = PublisherTestHelpers.Build();
        var ba = Guid.NewGuid();
        snaps.Map[ba] = PublisherTestHelpers.Snapshot(ba, externalTenantId: "not-a-guid");
        var r = await pub.PublishForBillingAccountAsync(ba, default);
        r.Outcome.Should().Be(PublishEntitlementOutcome.Skipped);
        r.Reason.Should().Be("external-tenant-id-not-a-guid");
        http.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task Successful_post_returns_published_with_status_and_sends_required_headers()
    {
        var (pub, http, snaps, _, _, _) = PublisherTestHelpers.Build(
            handler: new FakeHttpMessageHandler(HttpStatusCode.OK, "{}"));
        var ba = Guid.NewGuid();
        var tenant = Guid.NewGuid();
        snaps.Map[ba] = PublisherTestHelpers.Snapshot(ba, tenant.ToString());

        var r = await pub.PublishForBillingAccountAsync(ba, default);

        r.Outcome.Should().Be(PublishEntitlementOutcome.Published);
        r.HttpStatus.Should().Be(200);
        r.TenantId.Should().Be(tenant);

        http.Requests.Should().HaveCount(1);
        var req = http.Requests[0];
        req.Method.Should().Be(HttpMethod.Post);
        req.RequestUri!.AbsoluteUri.Should().Be(
            "http://tenant-billing.test/api/tenant-billing/entitlements/apply");
        req.Headers.GetValues("X-Tenant-Id").Should().ContainSingle().Which
            .Should().Be(tenant.ToString("D"));
        req.Headers.GetValues("X-Internal-Token").Should().ContainSingle().Which
            .Should().Be("tok");

        http.RequestBodies[0].Should().Contain(ba.ToString())
            .And.Contain("\"entitlementStatus\":\"Enabled\"");
    }

    [Fact]
    public async Task Status_401_returns_failed_with_token_hint()
    {
        var (pub, _, snaps, _, _, _) = PublisherTestHelpers.Build(
            handler: new FakeHttpMessageHandler(HttpStatusCode.Unauthorized, "nope"));
        var ba = Guid.NewGuid();
        snaps.Map[ba] = PublisherTestHelpers.Snapshot(ba, Guid.NewGuid().ToString());
        var r = await pub.PublishForBillingAccountAsync(ba, default);
        r.Outcome.Should().Be(PublishEntitlementOutcome.Failed);
        r.HttpStatus.Should().Be(401);
        r.Reason.Should().Be("tenant-billing-401-internal-token-rejected");
        r.ResponseBodySummary.Should().Be("nope");
    }

    [Fact]
    public async Task Status_404_returns_failed_with_no_profile_reason()
    {
        var (pub, _, snaps, _, _, _) = PublisherTestHelpers.Build(
            handler: new FakeHttpMessageHandler(HttpStatusCode.NotFound, "{\"err\":\"x\"}"));
        var ba = Guid.NewGuid();
        snaps.Map[ba] = PublisherTestHelpers.Snapshot(ba, Guid.NewGuid().ToString());
        var r = await pub.PublishForBillingAccountAsync(ba, default);
        r.Outcome.Should().Be(PublishEntitlementOutcome.Failed);
        r.HttpStatus.Should().Be(404);
        r.Reason.Should().Be("tenant-billing-404-no-profile-for-billing-account");
    }

    [Fact]
    public async Task Status_409_returns_failed_with_mismatch_reason()
    {
        var (pub, _, snaps, _, _, _) = PublisherTestHelpers.Build(
            handler: new FakeHttpMessageHandler(HttpStatusCode.Conflict));
        var ba = Guid.NewGuid();
        snaps.Map[ba] = PublisherTestHelpers.Snapshot(ba, Guid.NewGuid().ToString());
        var r = await pub.PublishForBillingAccountAsync(ba, default);
        r.Outcome.Should().Be(PublishEntitlementOutcome.Failed);
        r.HttpStatus.Should().Be(409);
        r.Reason.Should().Be("tenant-billing-409-profile-mismatch-or-closed");
    }

    [Fact]
    public async Task Transport_exception_returns_failed_without_throwing()
    {
        var (pub, _, snaps, _, _, _) = PublisherTestHelpers.Build(
            handler: FakeHttpMessageHandler.Throws(new HttpRequestException("connection refused")));
        var ba = Guid.NewGuid();
        snaps.Map[ba] = PublisherTestHelpers.Snapshot(ba, Guid.NewGuid().ToString());

        var r = await pub.PublishForBillingAccountAsync(ba, default);

        r.Outcome.Should().Be(PublishEntitlementOutcome.Failed);
        r.Reason.Should().Be("tenant-billing-unreachable");
        r.ResponseBodySummary.Should().Contain("connection refused");
    }

    [Fact]
    public async Task Status_500_returns_failed_with_generic_status_reason()
    {
        var (pub, _, snaps, _, _, _) = PublisherTestHelpers.Build(
            handler: new FakeHttpMessageHandler(HttpStatusCode.InternalServerError, "boom"));
        var ba = Guid.NewGuid();
        snaps.Map[ba] = PublisherTestHelpers.Snapshot(ba, Guid.NewGuid().ToString());

        var r = await pub.PublishForBillingAccountAsync(ba, default);

        r.Outcome.Should().Be(PublishEntitlementOutcome.Failed);
        r.HttpStatus.Should().Be(500);
        r.Reason.Should().Be("tenant-billing-500");
    }

    [Fact]
    public async Task Status_503_returns_failed_with_generic_status_reason()
    {
        var (pub, _, snaps, _, _, _) = PublisherTestHelpers.Build(
            handler: new FakeHttpMessageHandler(HttpStatusCode.ServiceUnavailable));
        var ba = Guid.NewGuid();
        snaps.Map[ba] = PublisherTestHelpers.Snapshot(ba, Guid.NewGuid().ToString());

        var r = await pub.PublishForBillingAccountAsync(ba, default);

        r.Outcome.Should().Be(PublishEntitlementOutcome.Failed);
        r.HttpStatus.Should().Be(503);
        r.Reason.Should().Be("tenant-billing-503");
    }

    [Fact]
    public async Task Empty_base_url_returns_failed_config_reason()
    {
        var (pub, http, snaps, _, _, _) = PublisherTestHelpers.Build(baseUrl: "");
        var ba = Guid.NewGuid();
        snaps.Map[ba] = PublisherTestHelpers.Snapshot(ba, Guid.NewGuid().ToString());
        var r = await pub.PublishForBillingAccountAsync(ba, default);
        r.Outcome.Should().Be(PublishEntitlementOutcome.Failed);
        r.Reason.Should().Be("base-url-not-configured");
        http.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task Empty_internal_token_returns_failed_config_reason()
    {
        var (pub, http, snaps, _, _, _) = PublisherTestHelpers.Build(token: "");
        var ba = Guid.NewGuid();
        snaps.Map[ba] = PublisherTestHelpers.Snapshot(ba, Guid.NewGuid().ToString());
        var r = await pub.PublishForBillingAccountAsync(ba, default);
        r.Outcome.Should().Be(PublishEntitlementOutcome.Failed);
        r.Reason.Should().Be("internal-token-not-configured");
        http.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task PublishSnapshotAsync_with_empty_tenant_id_skips()
    {
        var (pub, http, _, _, _, _) = PublisherTestHelpers.Build();
        var snap = PublisherTestHelpers.Snapshot(externalTenantId: Guid.NewGuid().ToString());
        var r = await pub.PublishSnapshotAsync(snap, Guid.Empty, default);
        r.Outcome.Should().Be(PublishEntitlementOutcome.Skipped);
        r.Reason.Should().Be("tenant-id-empty");
        http.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task PublishSnapshotAsync_when_disabled_skips_even_with_valid_input()
    {
        var (pub, http, _, _, _, _) = PublisherTestHelpers.Build(enabled: false);
        var snap = PublisherTestHelpers.Snapshot(externalTenantId: Guid.NewGuid().ToString());
        var r = await pub.PublishSnapshotAsync(snap, Guid.NewGuid(), default);
        r.Outcome.Should().Be(PublishEntitlementOutcome.Skipped);
        r.Reason.Should().Be("publisher-disabled");
        http.Requests.Should().BeEmpty();
    }
}
