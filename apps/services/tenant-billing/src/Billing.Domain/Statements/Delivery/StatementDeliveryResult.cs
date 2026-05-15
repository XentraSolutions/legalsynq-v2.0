namespace Billing.Domain.Statements.Delivery;

/// <summary>
/// MS-BILL-INT-001 — Deterministic outcome of a single
/// <see cref="IStatementDeliveryProvider.SendAsync"/> call.
///
/// <para>
/// Contract guarantees:
/// </para>
/// <list type="bullet">
///   <item><see cref="Status"/> is always one of
///   <see cref="StatementDeliveryStatus"/>.</item>
///   <item><see cref="Provider"/> is always populated (the orchestrator
///   uses it to label the persisted delivery row).</item>
///   <item><see cref="FailureReason"/> is non-null when
///   <see cref="Status"/> != <see cref="StatementDeliveryStatus.Sent"/>
///   so the UI can render a deterministic short reason without
///   parsing prose.</item>
///   <item>Providers MUST NOT throw for predictable failures —
///   they must return this record. Throwing surfaces as
///   <see cref="StatementDeliveryStatus.Failed"/> at the orchestrator
///   level with the exception type name as the reason.</item>
/// </list>
/// </summary>
public sealed record StatementDeliveryResult(
    string Status,
    string Provider,
    string? DeliveryId,
    string? CorrelationId,
    string? FailureReason)
{
    public bool IsSuccess => Status == StatementDeliveryStatus.Sent;

    public static StatementDeliveryResult ProviderNotConfigured(
        string provider, string correlationId) =>
        new(
            Status: StatementDeliveryStatus.ProviderUnavailable,
            Provider: provider,
            DeliveryId: null,
            CorrelationId: correlationId,
            FailureReason: "ProviderNotConfigured");

    public static StatementDeliveryResult InvalidRecipient(
        string provider, string correlationId, string reason) =>
        new(
            Status: StatementDeliveryStatus.InvalidRecipient,
            Provider: provider,
            DeliveryId: null,
            CorrelationId: correlationId,
            FailureReason: reason);

    public static StatementDeliveryResult Sent(
        string provider, string correlationId, string? deliveryId) =>
        new(
            Status: StatementDeliveryStatus.Sent,
            Provider: provider,
            DeliveryId: deliveryId,
            CorrelationId: correlationId,
            FailureReason: null);

    public static StatementDeliveryResult Failed(
        string provider, string correlationId, string reason) =>
        new(
            Status: StatementDeliveryStatus.Failed,
            Provider: provider,
            DeliveryId: null,
            CorrelationId: correlationId,
            FailureReason: reason);

    /// <summary>
    /// MS-BILL-INT-002 — Provider returned an explicit "try again"
    /// signal (transient 5xx, 429 rate-limit, network timeout).
    /// The controller maps this to HTTP 503 alongside
    /// <see cref="StatementDeliveryStatus.ProviderUnavailable"/>;
    /// the UI surfaces a "try again shortly" affordance and keeps
    /// the idempotency key live so the operator's retry stays the
    /// same logical attempt.
    /// </summary>
    public static StatementDeliveryResult RetryableFailure(
        string provider, string correlationId, string reason) =>
        new(
            Status: StatementDeliveryStatus.RetryableFailure,
            Provider: provider,
            DeliveryId: null,
            CorrelationId: correlationId,
            FailureReason: reason);

    /// <summary>
    /// MS-BILL-INT-003 — Governance short-circuit. Created by the
    /// orchestrator when <see cref="StatementRetryability"/> rejects
    /// the click BEFORE invoking the provider. <see cref="Provider"/>
    /// reflects whichever provider is currently bound (so the row,
    /// if persisted later, stays attributable); the snapshot row
    /// itself is NOT mutated on a RetryNotAllowed outcome — the
    /// pre-existing last-attempt state remains the source of truth.
    /// </summary>
    public static StatementDeliveryResult RetryNotAllowed(
        string provider, string correlationId, string reason) =>
        new(
            Status: StatementDeliveryStatus.RetryNotAllowed,
            Provider: provider,
            DeliveryId: null,
            CorrelationId: correlationId,
            FailureReason: reason);
}
