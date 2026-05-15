namespace TenantBilling.Domain.Entities;

/// <summary>
/// Payment recorded against an Invoice. B01 stores the payment record only;
/// invoice status transitions and totals reconciliation arrive in later blocks.
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
    /// </summary>
    public string? Notes { get; set; }

    public Invoice? Invoice { get; set; }
}
