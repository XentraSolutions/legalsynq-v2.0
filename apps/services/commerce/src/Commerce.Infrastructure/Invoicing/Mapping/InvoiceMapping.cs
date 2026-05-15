using Commerce.Contracts.Invoicing;
using Commerce.Domain.Invoicing;

namespace Commerce.Infrastructure.Invoicing.Mapping;

internal static class InvoiceMapping
{
    public static InvoiceLineResponse ToResponse(this InvoiceLine l) => new(
        l.Id, l.InvoiceId, l.SubscriptionItemId, l.Description, l.Quantity,
        l.UnitAmountMinor, l.LineAmountMinor, l.Currency,
        l.ServicePeriodStartUtc, l.ServicePeriodEndUtc, l.CreatedAtUtc);

    public static InvoiceResponse ToResponse(this Invoice i, IReadOnlyList<InvoiceLine> lines) => new(
        i.Id, i.BillingAccountId, i.SubscriptionId, i.InvoiceNumber, i.Status, i.Currency,
        i.SubtotalAmountMinor, i.DiscountAmountMinor, i.TaxAmountMinor, i.TotalAmountMinor,
        i.AmountPaidMinor, i.AmountDueMinor, i.IssueDateUtc, i.DueDateUtc,
        i.PaidAtUtc, i.VoidedAtUtc, i.Provider, i.ProviderInvoiceId,
        i.CreatedAtUtc, i.UpdatedAtUtc,
        lines.Select(l => l.ToResponse()).ToList());
}
