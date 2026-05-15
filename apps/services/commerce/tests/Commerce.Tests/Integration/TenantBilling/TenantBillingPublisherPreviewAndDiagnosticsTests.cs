using FluentAssertions;
using Xunit;

namespace Commerce.Tests.Integration.TenantBilling;

/// <summary>
/// TB-INT-02 — preview + diagnostics unit tests.
/// </summary>
public class TenantBillingPublisherPreviewAndDiagnosticsTests
{
    // ─── Preview ───

    [Fact]
    public async Task Preview_returns_null_when_billing_account_unknown()
    {
        var (pub, http, _, _, _, _) = PublisherTestHelpers.Build();
        var r = await pub.PreviewForBillingAccountAsync(Guid.NewGuid(), default);
        r.Should().BeNull();
        http.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task Preview_returns_payload_and_tenant_id_when_resolvable()
    {
        var (pub, http, snaps, _, _, _) = PublisherTestHelpers.Build();
        var ba = Guid.NewGuid();
        var tenant = Guid.NewGuid();
        snaps.Map[ba] = PublisherTestHelpers.Snapshot(ba, tenant.ToString());

        var r = await pub.PreviewForBillingAccountAsync(ba, default);

        r.Should().NotBeNull();
        r!.BillingAccountId.Should().Be(ba);
        r.TenantId.Should().Be(tenant);
        r.CanPublish.Should().BeTrue();
        r.SkipReason.Should().BeNull();
        r.TenantBillingPayload.Should().NotBeNull();
        r.TenantBillingPayload!.BillingAccountId.Should().Be(ba);
        r.TenantBillingPayload.SourceSystem.Should().Be("commerce");
        r.TenantBillingPayload.EntitlementStatus.Should().Be("Enabled");
        // Critically: no HTTP call.
        http.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task Preview_returns_skip_reason_when_external_tenant_id_missing()
    {
        var (pub, _, snaps, _, _, _) = PublisherTestHelpers.Build();
        var ba = Guid.NewGuid();
        snaps.Map[ba] = PublisherTestHelpers.Snapshot(ba, externalTenantId: null);

        var r = await pub.PreviewForBillingAccountAsync(ba, default);

        r.Should().NotBeNull();
        r!.CanPublish.Should().BeFalse();
        r.SkipReason.Should().Be("no-external-tenant-id");
        r.TenantId.Should().BeNull();
        r.TenantBillingPayload.Should().BeNull();
    }

    [Fact]
    public async Task Preview_returns_skip_reason_when_external_tenant_id_not_a_guid()
    {
        var (pub, _, snaps, _, _, _) = PublisherTestHelpers.Build();
        var ba = Guid.NewGuid();
        snaps.Map[ba] = PublisherTestHelpers.Snapshot(ba, externalTenantId: "not-a-guid");

        var r = await pub.PreviewForBillingAccountAsync(ba, default);

        r!.CanPublish.Should().BeFalse();
        r.SkipReason.Should().Be("external-tenant-id-not-a-guid");
        r.TenantBillingPayload.Should().BeNull();
    }

    [Fact]
    public async Task Preview_when_publisher_disabled_still_returns_payload_with_skip_reason()
    {
        var (pub, http, snaps, _, _, _) = PublisherTestHelpers.Build(enabled: false);
        var ba = Guid.NewGuid();
        snaps.Map[ba] = PublisherTestHelpers.Snapshot(ba, Guid.NewGuid().ToString());

        var r = await pub.PreviewForBillingAccountAsync(ba, default);

        r.Should().NotBeNull();
        r!.CanPublish.Should().BeFalse();
        r.SkipReason.Should().Be("publisher-disabled");
        r.TenantBillingPayload.Should().NotBeNull();
        http.Requests.Should().BeEmpty();
    }

    // ─── Diagnostics ───

    [Fact]
    public async Task Diagnostics_disabled_mode_when_publisher_disabled()
    {
        var (pub, _, _, _, _, _) = PublisherTestHelpers.Build(enabled: false);
        var d = await pub.GetDiagnosticsAsync(default);
        d.Mode.Should().Be("Disabled");
        d.Enabled.Should().BeFalse();
        d.TargetRoute.Should().Be("/api/tenant-billing/entitlements/apply");
    }

    [Fact]
    public async Task Diagnostics_ready_mode_when_fully_configured()
    {
        var (pub, _, _, _, _, _) = PublisherTestHelpers.Build();
        var d = await pub.GetDiagnosticsAsync(default);
        d.Mode.Should().Be("Ready");
        d.Enabled.Should().BeTrue();
        d.BaseUrlConfigured.Should().BeTrue();
        d.InternalTokenConfigured.Should().BeTrue();
    }

    [Fact]
    public async Task Diagnostics_misconfigured_mode_when_base_url_missing()
    {
        var (pub, _, _, _, _, _) = PublisherTestHelpers.Build(baseUrl: "");
        var d = await pub.GetDiagnosticsAsync(default);
        d.Mode.Should().Be("Misconfigured");
        d.BaseUrlConfigured.Should().BeFalse();
        d.InternalTokenConfigured.Should().BeTrue();
    }

    [Fact]
    public async Task Diagnostics_misconfigured_mode_when_token_missing()
    {
        var (pub, _, _, _, _, _) = PublisherTestHelpers.Build(token: "");
        var d = await pub.GetDiagnosticsAsync(default);
        d.Mode.Should().Be("Misconfigured");
        d.InternalTokenConfigured.Should().BeFalse();
    }

    [Fact]
    public async Task Diagnostics_reflects_retry_and_circuit_settings()
    {
        var (pub, _, _, _, _, _) = PublisherTestHelpers.Build(
            retryAttempts: 4,
            retryDelayMs: 750,
            circuitBreakerEnabled: true,
            circuitBreakerFailures: 7,
            circuitBreakerDurationSeconds: 120);
        var d = await pub.GetDiagnosticsAsync(default);
        d.RetryAttempts.Should().Be(4);
        d.RetryDelayMilliseconds.Should().Be(750);
        d.CircuitBreakerEnabled.Should().BeTrue();
        d.CircuitBreakerFailures.Should().Be(7);
        d.CircuitBreakerDurationSeconds.Should().Be(120);
        d.CircuitBreakerState.Should().Be("Closed");
    }

    [Fact]
    public async Task Diagnostics_does_not_expose_internal_token_via_serialisation()
    {
        var (pub, _, _, _, _, _) = PublisherTestHelpers.Build(
            token: "super-secret-token-1234567890");
        var d = await pub.GetDiagnosticsAsync(default);
        // The diagnostics record has no field that carries the token; the
        // only signal is the boolean. Serialise it and assert the secret
        // does not appear.
        var json = System.Text.Json.JsonSerializer.Serialize(d);
        json.Should().NotContain("super-secret-token");
        json.Should().NotContain("InternalToken\":\"");
        d.InternalTokenConfigured.Should().BeTrue();
    }
}
