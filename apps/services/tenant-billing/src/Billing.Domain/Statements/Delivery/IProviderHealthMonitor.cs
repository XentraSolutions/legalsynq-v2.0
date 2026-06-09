namespace Billing.Domain.Statements.Delivery;

/// <summary>
/// MS-BILL-INT-003 — Lightweight, in-memory, single-process
/// provider health signal. Records every send outcome on a rolling
/// time window and exposes a deterministic Healthy / Degraded /
/// Unavailable label for operator visibility.
///
/// <para>
/// This is intentionally NOT a distributed health mesh, NOT a
/// failover trigger, and NOT shared across instances. The signal
/// powers the tenant UI's provider-health pill and the runbook's
/// "is the upstream sick?" question; it never gates the actual
/// send (the provider's own response is still the source of
/// truth for any single attempt).
/// </para>
///
/// <para>
/// All implementations MUST be thread-safe; the orchestrator
/// records outcomes from concurrent request scopes.
/// </para>
/// </summary>
public interface IProviderHealthMonitor
{
    /// <summary>
    /// Record the deterministic outcome of a single send attempt.
    /// Called by the orchestrator AFTER the provider returns
    /// (whether it returned a result or threw). Must never throw.
    /// </summary>
    void RecordOutcome(string providerName, string deliveryStatus, DateTime nowUtc);

    /// <summary>
    /// Read the current rolling-window snapshot. Pure / read-only.
    /// </summary>
    ProviderHealthSnapshot GetHealth(DateTime nowUtc);
}

/// <summary>
/// MS-BILL-INT-003 — Closed set of provider-health labels rendered
/// by the tenant UI. Mirrored 1:1 in TypeScript.
/// </summary>
public static class ProviderHealthState
{
    /// <summary>No / few recent failures.</summary>
    public const string Healthy = "Healthy";

    /// <summary>
    /// Recent failures crossed
    /// <see cref="ProviderHealthOptions.DegradedAfterFailures"/>.
    /// Operator should expect transient errors but resend is still
    /// allowed.
    /// </summary>
    public const string Degraded = "Degraded";

    /// <summary>
    /// Recent failures crossed
    /// <see cref="ProviderHealthOptions.UnavailableAfterFailures"/>.
    /// The next click will probably fail; the UI shows a "provider
    /// looks down" banner but does NOT block the click (the
    /// operator owns that decision).
    /// </summary>
    public const string Unavailable = "Unavailable";

    public static bool IsValid(string? value) =>
        value is Healthy or Degraded or Unavailable;
}

/// <summary>
/// MS-BILL-INT-003 — Snapshot of the rolling-window state at
/// <paramref name="ObservedAtUtc"/>.
/// </summary>
/// <param name="State">
/// One of <see cref="ProviderHealthState"/>.
/// </param>
/// <param name="RecentFailures">
/// Count of failure-class outcomes inside the current window.
/// </param>
/// <param name="RecentSuccesses">
/// Count of <see cref="StatementDeliveryStatus.Sent"/> outcomes
/// inside the current window.
/// </param>
/// <param name="WindowSeconds">
/// The window width used to compute this snapshot, surfaced for
/// operator-facing copy ("3 failures in the last 60s").
/// </param>
/// <param name="ObservedAtUtc">When the snapshot was taken.</param>
public sealed record ProviderHealthSnapshot(
    string State,
    int RecentFailures,
    int RecentSuccesses,
    int WindowSeconds,
    DateTime ObservedAtUtc);
