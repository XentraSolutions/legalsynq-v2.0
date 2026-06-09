namespace Billing.Domain.Statements.Delivery;

/// <summary>
/// MS-BILL-INT-003 — Bound options for resend governance.
/// Synchronous, operator-driven. There is intentionally no async
/// worker, queue, scheduled retry, or background scheduler — these
/// values gate the next operator click only.
///
/// <para>
/// Section: <c>Billing:Delivery:Retry</c>. Every field has a safe
/// default so a deployment that ships without the section still
/// gets sane resend governance instead of disabled-everywhere or
/// unbounded-everywhere behaviour.
/// </para>
///
/// <para>Safe defaults:</para>
/// <list type="bullet">
///   <item><see cref="MaxAttempts"/> = 5 (covers a typical
///   transient-failure burst without becoming a vector for
///   accidental spam).</item>
///   <item><see cref="CooldownSeconds"/> = 60 (prevents same-second
///   double-click; tunable per deployment).</item>
///   <item><see cref="ProviderHealth"/> defaults: 60-second window,
///   degraded after 3 failures, unavailable after 6.</item>
/// </list>
/// </summary>
public sealed class StatementRetryOptions
{
    public const string SectionName = "Billing:Delivery:Retry";

    /// <summary>
    /// Hard cap on the persisted <c>DeliveryRetryCount</c>. Once a
    /// snapshot has had this many attempts (regardless of outcome
    /// per attempt), further resends are rejected with
    /// <see cref="StatementDeliveryStatus.RetryNotAllowed"/> and
    /// reason <c>RetryLimitReached</c>. The first attempt does not
    /// count against the cap (the cap is checked BEFORE incrementing).
    /// </summary>
    public int MaxAttempts { get; set; } = 5;

    /// <summary>
    /// Minimum number of seconds between consecutive attempts on
    /// the same snapshot. Computed as
    /// <c>DeliveryAttemptedAtUtc + CooldownSeconds</c>; reads at the
    /// orchestrator and is also surfaced into the read projection so
    /// the UI can render a deterministic countdown without a clock
    /// of its own. A value of <c>0</c> disables cooldown entirely
    /// (NOT recommended outside tests).
    /// </summary>
    public int CooldownSeconds { get; set; } = 60;

    /// <summary>
    /// Lightweight in-memory provider-health rolling window. See
    /// <see cref="ProviderHealthOptions"/>. Surfaces a
    /// process-local Healthy / Degraded / Unavailable signal into
    /// the contract projection so operators can see provider
    /// stress before they click Re-send.
    /// </summary>
    public ProviderHealthOptions ProviderHealth { get; set; } = new();
}

/// <summary>
/// MS-BILL-INT-003 — Tunables for the in-memory provider-health
/// monitor. Lightweight by design: a single rolling window of
/// recent send outcomes per process. There is intentionally no
/// distributed health mesh, no shared state, and no failover —
/// the signal exists for operator visibility only.
/// </summary>
public sealed class ProviderHealthOptions
{
    /// <summary>Sliding window in seconds (default 60).</summary>
    public int WindowSeconds { get; set; } = 60;

    /// <summary>
    /// Failure count within <see cref="WindowSeconds"/> at which
    /// the monitor flips from Healthy to Degraded. Default 3.
    /// </summary>
    public int DegradedAfterFailures { get; set; } = 3;

    /// <summary>
    /// Failure count within <see cref="WindowSeconds"/> at which
    /// the monitor flips from Degraded to Unavailable. Default 6.
    /// MUST be greater than or equal to
    /// <see cref="DegradedAfterFailures"/>; if misconfigured the
    /// monitor falls back to <c>DegradedAfterFailures</c>.
    /// </summary>
    public int UnavailableAfterFailures { get; set; } = 6;
}
