using Billing.Domain.Entities;

namespace Billing.Domain.Statements.Delivery;

/// <summary>
/// MS-BILL-INT-003 — Outcome of a single
/// <see cref="IStatementDeliveryService.SendAsync"/> call.
///
/// <para>
/// Carries the post-attempt <see cref="Snapshot"/> AND a typed
/// signal describing whether the orchestrator actually invoked
/// the provider or short-circuited on retry-governance.
/// </para>
///
/// <list type="bullet">
///   <item><see cref="Rejection"/> = <c>null</c> → orchestrator
///   ran the provider; <see cref="Snapshot"/> reflects the
///   persisted post-attempt state (DeliveryStatus etc updated).</item>
///   <item><see cref="Rejection"/> != <c>null</c> → governance
///   short-circuit; <see cref="Snapshot"/> is the un-mutated
///   prior state (controller maps to 429 / 409 with
///   <see cref="StatementDeliveryStatus.RetryNotAllowed"/> and
///   the rejection reason).</item>
/// </list>
/// </summary>
public sealed record StatementSendOutcome(
    CustomerStatement Snapshot,
    RetryDecision? Rejection);

/// <summary>
/// MS-BILL-INT-001 — Orchestrator surface that the
/// <c>StatementsController</c> calls instead of a provider directly.
/// Loads the immutable snapshot tenant-scoped, resolves the
/// recipient from the customer record, renders HTML from the
/// snapshot ONLY (never live tables), invokes the configured
/// provider, and persists the deterministic delivery outcome on
/// the snapshot row.
///
/// Returns the updated <see cref="CustomerStatement"/> so the
/// controller can return the post-attempt delivery state in a
/// single round-trip. Returns null when the snapshot does not
/// exist in tenant scope (controller maps to 404 — same shape as
/// the WRITE-009 / void / render flows so cross-tenant probes
/// surface as a uniform 404 with no enumeration signal).
/// </summary>
public interface IStatementDeliveryService
{
    Task<StatementSendOutcome?> SendAsync(
        Guid tenantId,
        Guid statementId,
        string? sentBy,
        CancellationToken ct = default);
}
