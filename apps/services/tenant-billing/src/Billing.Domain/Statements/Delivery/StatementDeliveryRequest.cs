using Billing.Domain.Entities;

namespace Billing.Domain.Statements.Delivery;

/// <summary>
/// MS-BILL-INT-001 — Provider-independent input for a delivery
/// attempt. The orchestrator builds this from the persisted
/// snapshot + customer record; providers see the rendered HTML
/// bytes and a recipient address only — they NEVER see the
/// <see cref="CustomerStatement"/> entity, the snapshot JSON, or
/// the Billing DbContext.
///
/// <see cref="CorrelationId"/> propagates through provider logs
/// and the persisted delivery row so a single send attempt is
/// traceable end-to-end.
/// </summary>
public sealed record StatementDeliveryRequest(
    Guid TenantId,
    Guid StatementId,
    string StatementNumber,
    string RecipientEmail,
    string RecipientName,
    string Subject,
    string HtmlBody,
    string FilenameHint,
    string CorrelationId);
