using Billing.Domain.Entities;

namespace Billing.Domain.Statements.Delivery;

/// <summary>
/// MS-BILL-INT-003 — Centralised, deterministic retryability
/// evaluator. ONE place decides "is the operator allowed to click
/// Re-send right now?", so the orchestrator, the controller's
/// pre-check, and the read projection all reach the same answer.
///
/// <para>Pure, stateless, no side effects. Inputs:</para>
/// <list type="bullet">
///   <item>The persisted last-attempt state on the snapshot row
///   (<see cref="CustomerStatement.DeliveryStatus"/>,
///   <see cref="CustomerStatement.DeliveryRetryCount"/>,
///   <see cref="CustomerStatement.DeliveryAttemptedAtUtc"/>).</item>
///   <item>The bound <see cref="StatementRetryOptions"/>.</item>
///   <item>A "now" supplied by the caller (TimeProvider in prod,
///   fake-time in tests).</item>
/// </list>
///
/// <para>
/// Status retryability matrix (closed set):
/// </para>
/// <list type="bullet">
///   <item><c>null</c> (never attempted) → retryable, no cooldown.</item>
///   <item><see cref="StatementDeliveryStatus.Sent"/> → retryable
///   (operator may legitimately re-send a successful statement)
///   subject to cooldown + retry-limit.</item>
///   <item><see cref="StatementDeliveryStatus.InvalidRecipient"/>
///   → NOT retryable (operator must fix the customer record first;
///   spamming Re-send won't help).</item>
///   <item><see cref="StatementDeliveryStatus.ProviderUnavailable"/>
///   → retryable subject to cooldown + retry-limit (banner already
///   tells the operator what to fix).</item>
///   <item><see cref="StatementDeliveryStatus.RetryableFailure"/>
///   → retryable subject to cooldown + retry-limit.</item>
///   <item><see cref="StatementDeliveryStatus.Failed"/> → retryable
///   subject to cooldown + retry-limit (provider may have
///   transient state — if it isn't, the next attempt re-confirms
///   Failed and the retry-limit eventually stops it).</item>
///   <item><see cref="StatementDeliveryStatus.RetryNotAllowed"/>
///   → derived state, never persisted as a "last outcome" — if it
///   somehow appears here, treat as retryable so the operator can
///   try again after the next cooldown window.</item>
/// </list>
/// </summary>
public static class StatementRetryability
{
    /// <summary>
    /// Reason codes used both by the retry rejection result and by
    /// the read projection. Closed set — the UI renders one banner
    /// per code so the surface is "one outcome, one message".
    /// </summary>
    public static class Reason
    {
        /// <summary>Last attempt is within the cooldown window.</summary>
        public const string CooldownActive = "CooldownActive";

        /// <summary>
        /// <see cref="StatementRetryOptions.MaxAttempts"/> reached.
        /// </summary>
        public const string RetryLimitReached = "RetryLimitReached";

        /// <summary>
        /// Last terminal outcome is non-retryable
        /// (<c>InvalidRecipient</c> today).
        /// </summary>
        public const string NonRetryableTerminal = "NonRetryableTerminal";
    }

    public static RetryDecision Evaluate(
        CustomerStatement snapshot,
        StatementRetryOptions options,
        DateTime nowUtc)
    {
        if (snapshot is null) throw new ArgumentNullException(nameof(snapshot));
        if (options is null) throw new ArgumentNullException(nameof(options));

        var status = snapshot.DeliveryStatus;
        var retryCount = snapshot.DeliveryRetryCount;
        var lastAttemptUtc = snapshot.DeliveryAttemptedAtUtc;

        // Defensive: hostile / corrupt config must not produce
        // negative limits (retryCount >= -1 is always true).
        var maxAttempts = options.MaxAttempts > 0 ? options.MaxAttempts : 1;
        var cooldownSeconds = options.CooldownSeconds >= 0
            ? options.CooldownSeconds
            : 0;
        var retriesRemaining = Math.Max(0, maxAttempts - retryCount);

        // 1. Non-retryable terminal — InvalidRecipient is the only
        //    state that cannot be helped by clicking again.
        if (status == StatementDeliveryStatus.InvalidRecipient)
        {
            return new RetryDecision(
                IsRetryable: false,
                Reason: Reason.NonRetryableTerminal,
                CooldownUntilUtc: null,
                RetriesRemaining: retriesRemaining);
        }

        // 2. Retry-limit cap — applies even to "Sent" so an operator
        //    can't accidentally spam Re-send 100 times on a healthy
        //    snapshot. First attempt (retryCount == 0) is always
        //    allowed regardless of the cap.
        if (retryCount > 0 && retryCount >= maxAttempts)
        {
            return new RetryDecision(
                IsRetryable: false,
                Reason: Reason.RetryLimitReached,
                CooldownUntilUtc: null,
                RetriesRemaining: 0);
        }

        // 3. Cooldown window — only applies when there has been
        //    at least one attempt and CooldownSeconds > 0.
        if (lastAttemptUtc.HasValue && cooldownSeconds > 0)
        {
            var cooldownUntil = DateTime.SpecifyKind(
                lastAttemptUtc.Value, DateTimeKind.Utc)
                .AddSeconds(cooldownSeconds);
            if (nowUtc < cooldownUntil)
            {
                return new RetryDecision(
                    IsRetryable: false,
                    Reason: Reason.CooldownActive,
                    CooldownUntilUtc: cooldownUntil,
                    RetriesRemaining: retriesRemaining);
            }
        }

        return new RetryDecision(
            IsRetryable: true,
            Reason: null,
            CooldownUntilUtc: null,
            RetriesRemaining: retriesRemaining);
    }
}

/// <summary>
/// MS-BILL-INT-003 — Result of
/// <see cref="StatementRetryability.Evaluate"/>. Surfaced into
/// the contract projection so the UI renders the same answer the
/// orchestrator would give without re-implementing the matrix
/// in TypeScript.
/// </summary>
/// <param name="IsRetryable">
/// <c>true</c> when the operator's next click would be allowed.
/// </param>
/// <param name="Reason">
/// One of <see cref="StatementRetryability.Reason"/> when
/// <see cref="IsRetryable"/> is <c>false</c>; <c>null</c> when
/// retryable.
/// </param>
/// <param name="CooldownUntilUtc">
/// Populated only when <see cref="Reason"/> ==
/// <see cref="StatementRetryability.Reason.CooldownActive"/>;
/// <c>null</c> otherwise.
/// </param>
/// <param name="RetriesRemaining">
/// <c>MaxAttempts - DeliveryRetryCount</c>, never negative.
/// Surfaced for operator visibility (e.g. "2 attempts left").
/// </param>
public sealed record RetryDecision(
    bool IsRetryable,
    string? Reason,
    DateTime? CooldownUntilUtc,
    int RetriesRemaining);
