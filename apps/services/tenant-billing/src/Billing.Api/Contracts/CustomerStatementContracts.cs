using System.ComponentModel.DataAnnotations;
using Billing.Domain.Entities;

namespace Billing.Api.Contracts;

/// <summary>
/// STAT-B02 — Request body for
/// <c>POST /api/statements/customers/{customerId}/generate</c>.
/// </summary>
public sealed class GenerateStatementRequest
{
    [Required]
    public DateTime? PeriodStart { get; set; }

    [Required]
    public DateTime? PeriodEnd { get; set; }

    /// <summary>
    /// Optional override; null falls back to the tenant's default
    /// template (if any).
    /// </summary>
    public Guid? TemplateId { get; set; }

    /// <summary>
    /// When true the persistence service also captures the rendered
    /// HTML on the snapshot. Default false — most callers will
    /// render lazily via <c>GET .../render/html</c>.
    /// </summary>
    public bool RenderHtml { get; set; }
}

/// <summary>
/// STAT-B02 — Request body for
/// <c>POST /api/statements/customers/{customerId}/monthly/generate</c>.
/// </summary>
public sealed class GenerateMonthlyStatementRequest
{
    [Required, Range(1900, 2999)]
    public int? Year { get; set; }

    [Required, Range(1, 12)]
    public int? Month { get; set; }

    public Guid? TemplateId { get; set; }
    public bool RenderHtml { get; set; }
}

/// <summary>
/// STAT-B02 — Optional body for the void endpoint.
/// </summary>
public sealed class VoidStatementRequest
{
    [StringLength(1000)]
    public string? Reason { get; set; }
}

/// <summary>
/// STAT-B02 — Persisted statement detail view. Includes all
/// monetary aggregates and snapshot blobs so a caller can fully
/// re-render without a second round-trip.
/// </summary>
public sealed record CustomerStatementResponse(
    Guid Id,
    Guid TenantId,
    Guid CustomerId,
    string StatementNumber,
    Guid? TemplateId,
    DateTime PeriodStart,
    DateTime PeriodEnd,
    DateTime GeneratedAtUtc,
    string Status,
    string Currency,
    decimal OpeningBalance,
    decimal ClosingBalance,
    decimal OutstandingBalance,
    decimal TotalInvoiced,
    decimal TotalPaid,
    string StatementSnapshotJson,
    string? TemplateSnapshotJson,
    bool HasHtmlSnapshot,
    DateTime? VoidedAtUtc,
    string? VoidReason,
    // ---- MS-BILL-INT-001 — delivery lifecycle (read-only) ----------
    StatementDeliveryStateResponse Delivery)
{
    public static CustomerStatementResponse From(CustomerStatement s) => new(
        Id: s.Id,
        TenantId: s.TenantId,
        CustomerId: s.CustomerId,
        StatementNumber: s.StatementNumber,
        TemplateId: s.TemplateId,
        PeriodStart: s.PeriodStart,
        PeriodEnd: s.PeriodEnd,
        GeneratedAtUtc: s.GeneratedAtUtc,
        Status: s.Status,
        Currency: s.Currency,
        OpeningBalance: s.OpeningBalance,
        ClosingBalance: s.ClosingBalance,
        OutstandingBalance: s.OutstandingBalance,
        TotalInvoiced: s.TotalInvoiced,
        TotalPaid: s.TotalPaid,
        StatementSnapshotJson: s.StatementSnapshotJson,
        TemplateSnapshotJson: s.TemplateSnapshotJson,
        HasHtmlSnapshot: !string.IsNullOrEmpty(s.HtmlSnapshot),
        VoidedAtUtc: s.VoidedAtUtc,
        VoidReason: s.VoidReason,
        Delivery: StatementDeliveryStateResponse.From(s));
}

/// <summary>
/// MS-BILL-INT-001 — Read-only projection of the most-recent
/// delivery attempt. Always populated (never null) so the UI does
/// not need a tri-state defensive guard. <see cref="Status"/> is
/// null when there has been no attempt yet.
///
/// Snapshot CONTENT is immutable; this projection only describes
/// delivery lifecycle and is safe to render alongside the
/// snapshot blobs.
///
/// MS-BILL-INT-003 — <see cref="Retryability"/> and
/// <see cref="ProviderHealth"/> are populated by call sites that
/// have access to <c>StatementRetryOptions</c> + the
/// <c>IProviderHealthMonitor</c> (the snapshot-detail GET and the
/// send POST). The list / customer-history GETs leave them
/// <c>null</c> — the UI gracefully falls back to "retryable, no
/// cooldown, health unknown" for list rows.
/// </summary>
public sealed record StatementDeliveryStateResponse(
    string? Status,
    string? Provider,
    string? FailureReason,
    string? RecipientEmail,
    string? SentBy,
    string? DeliveryId,
    string? CorrelationId,
    DateTime? AttemptedAtUtc,
    DateTime? LastSentAtUtc,
    int RetryCount,
    bool HasEverBeenSent,
    bool ProviderConfigured,
    StatementRetryabilityResponse? Retryability,
    ProviderHealthResponse? ProviderHealth)
{
    public static StatementDeliveryStateResponse From(CustomerStatement s) =>
        From(s, retryability: null, providerHealth: null);

    public static StatementDeliveryStateResponse From(
        CustomerStatement s,
        StatementRetryabilityResponse? retryability,
        ProviderHealthResponse? providerHealth) => new(
        Status: s.DeliveryStatus,
        Provider: s.DeliveryProvider,
        FailureReason: s.DeliveryFailureReason,
        RecipientEmail: s.DeliveryRecipientEmail,
        SentBy: s.DeliverySentBy,
        DeliveryId: s.DeliveryId,
        CorrelationId: s.DeliveryCorrelationId,
        AttemptedAtUtc: s.DeliveryAttemptedAtUtc,
        LastSentAtUtc: s.DeliveryLastSentAtUtc,
        RetryCount: s.DeliveryRetryCount,
        HasEverBeenSent: s.DeliveryLastSentAtUtc.HasValue,
        // The "ProviderUnavailable + reason ProviderNotConfigured"
        // pair is the deterministic signal the UI uses to surface
        // the documented "Email delivery is not configured yet"
        // banner. Anything else (Sent / Failed / RetryableFailure /
        // InvalidRecipient) implies a provider IS configured.
        ProviderConfigured: !(
            s.DeliveryStatus == Billing.Domain.Statements.Delivery.StatementDeliveryStatus.ProviderUnavailable
            && s.DeliveryFailureReason == "ProviderNotConfigured"),
        Retryability: retryability,
        ProviderHealth: providerHealth);
}

/// <summary>
/// MS-BILL-INT-003 — Mirror of
/// <see cref="Billing.Domain.Statements.Delivery.RetryDecision"/>
/// over the wire. The UI consumes this projection to gate the
/// Re-send button and render a deterministic countdown / banner
/// without re-implementing the retryability matrix.
/// </summary>
public sealed record StatementRetryabilityResponse(
    bool IsRetryable,
    string? Reason,
    DateTime? CooldownUntilUtc,
    int RetriesRemaining,
    int MaxAttempts);

/// <summary>
/// MS-BILL-INT-003 — Mirror of
/// <see cref="Billing.Domain.Statements.Delivery.ProviderHealthSnapshot"/>
/// over the wire. Process-local rolling-window signal exposed for
/// operator visibility only (never gates a click).
/// </summary>
public sealed record ProviderHealthResponse(
    string State,
    int RecentFailures,
    int RecentSuccesses,
    int WindowSeconds,
    DateTime ObservedAtUtc);

/// <summary>
/// MS-BILL-INT-001 — Response body for
/// <c>POST /api/statements/history/{id}/send</c>. Same shape on
/// success and failure so the UI parses one schema and switches on
/// <see cref="DeliveryStatus"/>. <see cref="Statement"/> is the
/// post-attempt snapshot so the caller does not need a follow-up
/// GET to re-render the delivery state.
/// </summary>
public sealed record StatementSendResponse(
    string DeliveryStatus,
    string Provider,
    bool ProviderConfigured,
    string? Reason,
    string? RecipientEmail,
    string? DeliveryId,
    string? CorrelationId,
    DateTime? AttemptedAtUtc,
    DateTime? SentAtUtc,
    int RetryCount,
    StatementRetryabilityResponse? Retryability,
    ProviderHealthResponse? ProviderHealth,
    CustomerStatementResponse Statement)
{
    /// <summary>
    /// Build the response over a post-attempt snapshot.
    /// <paramref name="overrideStatus"/> lets the controller surface
    /// the governance short-circuit
    /// (<see cref="Billing.Domain.Statements.Delivery.StatementDeliveryStatus.RetryNotAllowed"/>)
    /// and its <paramref name="overrideReason"/> WITHOUT mutating
    /// the persisted row (the orchestrator returns the un-mutated
    /// snapshot on RetryNotAllowed precisely so the prior outcome
    /// stays the source of truth on the row).
    /// </summary>
    public static StatementSendResponse From(
        CustomerStatement s,
        StatementRetryabilityResponse? retryability = null,
        ProviderHealthResponse? providerHealth = null,
        string? overrideStatus = null,
        string? overrideReason = null)
    {
        var status = overrideStatus
            ?? s.DeliveryStatus
            ?? Billing.Domain.Statements.Delivery.StatementDeliveryStatus.Failed;
        var reason = overrideReason ?? s.DeliveryFailureReason;
        var providerConfigured = !(
            (s.DeliveryStatus ?? overrideStatus) == Billing.Domain.Statements.Delivery.StatementDeliveryStatus.ProviderUnavailable
            && s.DeliveryFailureReason == "ProviderNotConfigured");

        return new StatementSendResponse(
            DeliveryStatus: status,
            Provider: s.DeliveryProvider ?? "unknown",
            ProviderConfigured: providerConfigured,
            Reason: reason,
            RecipientEmail: s.DeliveryRecipientEmail,
            DeliveryId: s.DeliveryId,
            CorrelationId: s.DeliveryCorrelationId,
            AttemptedAtUtc: s.DeliveryAttemptedAtUtc,
            SentAtUtc: s.DeliveryLastSentAtUtc,
            RetryCount: s.DeliveryRetryCount,
            Retryability: retryability,
            ProviderHealth: providerHealth,
            Statement: CustomerStatementResponse.From(s) with
            {
                Delivery = StatementDeliveryStateResponse.From(s, retryability, providerHealth),
            });
    }
}

/// <summary>
/// STAT-B02 — Lightweight projection for the per-customer history
/// list. Excludes snapshot blobs to keep the response small.
/// </summary>
public sealed record CustomerStatementSummaryResponse(
    Guid Id,
    string StatementNumber,
    Guid? TemplateId,
    DateTime PeriodStart,
    DateTime PeriodEnd,
    DateTime GeneratedAtUtc,
    string Status,
    string Currency,
    decimal ClosingBalance,
    decimal OutstandingBalance,
    bool HasHtmlSnapshot,
    DateTime? VoidedAtUtc)
{
    public static CustomerStatementSummaryResponse From(CustomerStatement s) => new(
        Id: s.Id,
        StatementNumber: s.StatementNumber,
        TemplateId: s.TemplateId,
        PeriodStart: s.PeriodStart,
        PeriodEnd: s.PeriodEnd,
        GeneratedAtUtc: s.GeneratedAtUtc,
        Status: s.Status,
        Currency: s.Currency,
        ClosingBalance: s.ClosingBalance,
        OutstandingBalance: s.OutstandingBalance,
        HasHtmlSnapshot: !string.IsNullOrEmpty(s.HtmlSnapshot),
        VoidedAtUtc: s.VoidedAtUtc);
}
