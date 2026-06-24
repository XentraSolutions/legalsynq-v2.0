namespace Billing.Domain.Statements.Delivery;

/// <summary>
/// MS-BILL-INT-001 — Deterministic outcome for a statement-delivery
/// attempt. Every <see cref="IStatementDeliveryProvider"/>
/// implementation MUST map to exactly one of these values; the
/// orchestrator persists the literal string and the API/UI render
/// off the same set so there is no ambiguity between "send failed"
/// vs "no provider configured" vs "provider rejected the address".
///
/// This set is intentionally closed and small. New states require
/// a coordinated migration + UI update; do not add ad-hoc states
/// from inside a single provider implementation.
/// </summary>
public static class StatementDeliveryStatus
{
    /// <summary>Provider accepted the message for delivery.</summary>
    public const string Sent = "Sent";

    /// <summary>
    /// Provider rejected the request for a request-shape reason
    /// (auth, bad payload, etc). Retry is allowed but unlikely to
    /// help without operator intervention.
    /// </summary>
    public const string Failed = "Failed";

    /// <summary>
    /// No provider is configured (the WRITE-009 placeholder branch),
    /// the configured provider's transport is down, or the provider
    /// itself returned a transient unavailable signal. The UI surfaces
    /// the documented "Email delivery is not configured yet" banner;
    /// retry is allowed and is expected to succeed once the operator
    /// wires a provider.
    /// </summary>
    public const string ProviderUnavailable = "ProviderUnavailable";

    /// <summary>
    /// Recipient email is missing on the customer record or fails
    /// the provider's address-shape check. Retry without operator
    /// intervention will not help; the operator must fix the
    /// customer record first.
    /// </summary>
    public const string InvalidRecipient = "InvalidRecipient";

    /// <summary>
    /// Provider returned an explicit "try again" signal (rate limit,
    /// 429, transient 5xx). Retry is the expected next action.
    /// </summary>
    public const string RetryableFailure = "RetryableFailure";

    /// <summary>
    /// MS-BILL-INT-003 — Governance short-circuit. The orchestrator
    /// rejected the click BEFORE invoking the provider because the
    /// snapshot is in cooldown, has reached the retry-limit cap, or
    /// the last terminal outcome is non-retryable
    /// (<see cref="InvalidRecipient"/>). The controller maps this
    /// to HTTP 429 with a <c>Retry-After</c> header on cooldown,
    /// 409 on retry-limit / non-retryable terminal. The persisted
    /// row is NOT touched on a RetryNotAllowed result — the
    /// pre-existing last-attempt state remains the truth.
    /// </summary>
    public const string RetryNotAllowed = "RetryNotAllowed";

    public static bool IsValid(string? value) =>
        value is Sent or Failed or ProviderUnavailable
            or InvalidRecipient or RetryableFailure or RetryNotAllowed;

    public static bool IsTerminalSuccess(string? value) =>
        value == Sent;
}
