using System.ComponentModel.DataAnnotations;
using Billing.Domain.Entities;
using Billing.Domain.Services;

namespace Billing.Api.Contracts;

public sealed class RefundInvoiceRequest
{
    [Required] public Guid TenantId { get; set; }

    [Range(typeof(decimal), "0.01", "79228162514264337593543950335")]
    public decimal Amount { get; set; }

    [MaxLength(3)] public string? Currency { get; set; }

    [MaxLength(1000)] public string? Reason { get; set; }

    public DateTime? RefundedAt { get; set; }
}

public sealed record RefundResponse(
    Guid Id,
    Guid TenantId,
    Guid InvoiceId,
    decimal Amount,
    string Currency,
    string? Reason,
    DateTime RefundedAt,
    DateTime CreatedAt)
{
    public static RefundResponse From(Refund r) => new(
        r.Id, r.TenantId, r.InvoiceId, r.Amount, r.Currency, r.Reason,
        r.RefundedAt, r.CreatedAt);
}

public sealed record RefundInvoiceResponse(RefundResponse Refund, InvoiceResponse Invoice)
{
    public static RefundInvoiceResponse From(RefundResult result) => new(
        RefundResponse.From(result.Refund),
        InvoiceResponse.From(result.Invoice));
}
