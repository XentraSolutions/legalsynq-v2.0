namespace Billing.Domain.Entities;

/// <summary>
/// Refund recorded against a previously Paid invoice. Refunds are tracked as
/// their own ledger entries (rather than negative payments) so the gross
/// payment history and the refund history remain independently auditable.
/// Multiple Refund rows may exist for one invoice (sequential partial refunds).
/// </summary>
public class Refund
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid InvoiceId { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "USD";
    public string? Reason { get; set; }
    public DateTime RefundedAt { get; set; }
    public DateTime CreatedAt { get; set; }

    public Invoice? Invoice { get; set; }
}
