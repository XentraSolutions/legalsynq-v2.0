using Commerce.Domain.Common;
using Commerce.Domain.Invoicing.Enums;
using Commerce.Domain.Payments.Enums;

namespace Commerce.Domain.Invoicing;

/// <summary>
/// First-class financial record owned by Commerce. The provider is
/// only a hint about which payment provider the invoice is expected to
/// be paid through; Commerce remains the source of truth for amounts,
/// status and number.
/// </summary>
public sealed class Invoice : Entity<Guid>
{
    public Guid BillingAccountId { get; private set; }
    public Guid? SubscriptionId { get; private set; }
    public string InvoiceNumber { get; private set; } = default!;
    public InvoiceStatus Status { get; private set; }
    public string Currency { get; private set; } = default!;
    public long SubtotalAmountMinor { get; private set; }
    public long DiscountAmountMinor { get; private set; }
    public long TaxAmountMinor { get; private set; }
    public long TotalAmountMinor { get; private set; }
    public long AmountPaidMinor { get; private set; }
    public long AmountDueMinor { get; private set; }
    public DateTime IssueDateUtc { get; private set; }
    public DateTime? DueDateUtc { get; private set; }
    public DateTime? PaidAtUtc { get; private set; }
    public DateTime? VoidedAtUtc { get; private set; }
    public PaymentProviderType? Provider { get; private set; }
    public string? ProviderInvoiceId { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    private Invoice() { }

    public static Invoice Create(
        Guid billingAccountId,
        Guid? subscriptionId,
        string invoiceNumber,
        string currency,
        DateTime issueDateUtc,
        DateTime? dueDateUtc,
        InvoiceStatus initialStatus,
        DateTime nowUtc)
    {
        if (billingAccountId == Guid.Empty)
            throw new InvalidOperationException("BillingAccountId is required.");
        if (string.IsNullOrWhiteSpace(invoiceNumber))
            throw new InvalidOperationException("InvoiceNumber is required.");
        if (string.IsNullOrWhiteSpace(currency) || currency.Length != 3)
            throw new InvalidOperationException("Currency must be a 3-letter code.");
        if (initialStatus is not (InvoiceStatus.Draft or InvoiceStatus.Open))
            throw new InvalidOperationException("New invoice must start as Draft or Open.");

        return new Invoice
        {
            Id = Guid.NewGuid(),
            BillingAccountId = billingAccountId,
            SubscriptionId = subscriptionId,
            InvoiceNumber = invoiceNumber.Trim(),
            Status = initialStatus,
            Currency = currency.ToUpperInvariant(),
            IssueDateUtc = issueDateUtc,
            DueDateUtc = dueDateUtc,
            CreatedAtUtc = nowUtc,
            UpdatedAtUtc = nowUtc
        };
    }

    public void Recalculate(IReadOnlyList<InvoiceLine> lines, DateTime nowUtc)
    {
        if (Status is InvoiceStatus.Paid or InvoiceStatus.Void)
            throw new InvalidOperationException("Paid or void invoice cannot be recalculated.");
        long subtotal = 0;
        foreach (var l in lines)
        {
            if (!string.Equals(l.Currency, Currency, StringComparison.Ordinal))
                throw new InvalidOperationException("Invoice line currency must match invoice currency.");
            subtotal += l.LineAmountMinor;
        }
        SubtotalAmountMinor = subtotal;
        TotalAmountMinor = SubtotalAmountMinor - DiscountAmountMinor + TaxAmountMinor;
        AmountDueMinor = Math.Max(0, TotalAmountMinor - AmountPaidMinor);
        UpdatedAtUtc = nowUtc;
    }

    public void AttachProviderInvoice(PaymentProviderType provider, string? providerInvoiceId, DateTime nowUtc)
    {
        Provider = provider;
        if (!string.IsNullOrWhiteSpace(providerInvoiceId))
            ProviderInvoiceId = providerInvoiceId.Trim();
        UpdatedAtUtc = nowUtc;
    }

    public void RegisterPayment(long amountMinor, DateTime nowUtc)
    {
        if (amountMinor < 0)
            throw new InvalidOperationException("Payment amount cannot be negative.");
        if (Status == InvoiceStatus.Void)
            throw new InvalidOperationException("Void invoice cannot be paid.");
        AmountPaidMinor += amountMinor;
        AmountDueMinor = Math.Max(0, TotalAmountMinor - AmountPaidMinor);
        if (AmountPaidMinor >= TotalAmountMinor && TotalAmountMinor > 0)
        {
            Status = InvoiceStatus.Paid;
            PaidAtUtc = nowUtc;
        }
        UpdatedAtUtc = nowUtc;
    }

    public void Void(DateTime nowUtc)
    {
        if (Status == InvoiceStatus.Paid)
            throw new InvalidOperationException("Paid invoice cannot be voided in COM-B06.");
        Status = InvoiceStatus.Void;
        VoidedAtUtc = nowUtc;
        UpdatedAtUtc = nowUtc;
    }
}
