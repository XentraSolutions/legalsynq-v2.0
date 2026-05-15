using Commerce.Domain.Common;

namespace Commerce.Domain.Invoicing;

public sealed class InvoiceLine : Entity<Guid>
{
    public Guid InvoiceId { get; private set; }
    public Guid? SubscriptionItemId { get; private set; }
    public string Description { get; private set; } = default!;
    public int Quantity { get; private set; }
    public long UnitAmountMinor { get; private set; }
    public long LineAmountMinor { get; private set; }
    public string Currency { get; private set; } = default!;
    public DateTime? ServicePeriodStartUtc { get; private set; }
    public DateTime? ServicePeriodEndUtc { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    private InvoiceLine() { }

    public static InvoiceLine Create(
        Guid invoiceId,
        Guid? subscriptionItemId,
        string description,
        int quantity,
        long unitAmountMinor,
        string currency,
        DateTime? servicePeriodStartUtc,
        DateTime? servicePeriodEndUtc,
        DateTime nowUtc)
    {
        if (invoiceId == Guid.Empty)
            throw new InvalidOperationException("InvoiceId is required.");
        if (string.IsNullOrWhiteSpace(description))
            throw new InvalidOperationException("Description is required.");
        if (quantity < 1)
            throw new InvalidOperationException("Quantity must be >= 1.");
        if (unitAmountMinor < 0)
            throw new InvalidOperationException("UnitAmountMinor cannot be negative.");
        if (string.IsNullOrWhiteSpace(currency) || currency.Length != 3)
            throw new InvalidOperationException("Currency must be a 3-letter code.");
        if (servicePeriodStartUtc.HasValue && servicePeriodEndUtc.HasValue
            && servicePeriodEndUtc.Value <= servicePeriodStartUtc.Value)
            throw new InvalidOperationException("ServicePeriodEnd must be after ServicePeriodStart.");

        return new InvoiceLine
        {
            Id = Guid.NewGuid(),
            InvoiceId = invoiceId,
            SubscriptionItemId = subscriptionItemId,
            Description = description.Trim(),
            Quantity = quantity,
            UnitAmountMinor = unitAmountMinor,
            LineAmountMinor = checked(quantity * unitAmountMinor),
            Currency = currency.ToUpperInvariant(),
            ServicePeriodStartUtc = servicePeriodStartUtc,
            ServicePeriodEndUtc = servicePeriodEndUtc,
            CreatedAtUtc = nowUtc
        };
    }
}
