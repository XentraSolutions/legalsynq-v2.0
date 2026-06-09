namespace Billing.Domain.Entities;

/// <summary>
/// Payment recorded against an Invoice. B01 stores the payment record only;
/// invoice status transitions and totals reconciliation arrive in later blocks.
///
/// MS-BILL-WRITE-002 lifecycle (Recorded → Voided):
/// the financial fields below (<see cref="Amount"/>, <see cref="Currency"/>,
/// <see cref="Method"/>, <see cref="PaidAt"/>, <see cref="TransactionReference"/>,
/// <see cref="Notes"/>, <see cref="CreatedAt"/>) are IMMUTABLE once the row is
/// created. A reversal flips <see cref="Status"/> to <c>"Voided"</c> and
/// populates the two append-only audit fields <see cref="ReversedAt"/> and
/// <see cref="ReversalReason"/>; the original money values are preserved
/// verbatim so the financial history remains auditable. This is enforced in
/// <c>PaymentService.ReverseAsync</c> and validated by tests.
/// </summary>
public class Payment
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid InvoiceId { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "USD";
    public string Method { get; set; } = string.Empty;
    public string Status { get; set; } = "Pending";
    public string? TransactionReference { get; set; }
    public DateTime PaidAt { get; set; }
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Free-form internal note recorded when the payment was captured (e.g.
    /// "wire received from accounts payable", check number reconciliation).
    /// Optional. Trimmed by the service layer before persistence.
    ///
    /// MS-BILL-WRITE-003 mutability exception: this is the ONLY non-financial
    /// field on the payment that can be edited after creation, via
    /// <c>PaymentService.UpdateNotesAsync</c>. Editing notes does NOT alter
    /// any financial field, status, lifecycle timestamp, or reversal audit
    /// fields, so it does not invalidate the immutable-financial-history
    /// guarantee. Notes can be edited on both Recorded and Voided payments
    /// (operators sometimes need to clarify reversal context after the fact).
    /// </summary>
    public string? Notes { get; set; }

    /// <summary>
    /// MS-BILL-WRITE-002 — UTC timestamp at which a tenant admin reversed
    /// this payment. Null on every Recorded payment; non-null IFF
    /// <see cref="Status"/> equals <c>"Voided"</c>. Append-only — once set,
    /// the reversal is permanent (the lifecycle is one-way).
    /// </summary>
    public DateTime? ReversedAt { get; set; }

    /// <summary>
    /// MS-BILL-WRITE-002 — required free-form reason captured when the
    /// payment was reversed (e.g. "duplicate ACH submission", "client
    /// dispute resolved"). Trimmed and length-bounded by the service
    /// layer; column max length is 1000 (see <c>BillingDbContext</c>).
    /// Null on every Recorded payment; non-null IFF <see cref="Status"/>
    /// equals <c>"Voided"</c>. Append-only.
    /// </summary>
    public string? ReversalReason { get; set; }

    public Invoice? Invoice { get; set; }
}
