using Commerce.Contracts.Payments;
using Commerce.Domain.Payments;

namespace Commerce.Infrastructure.Payments.Mapping;

internal static class PaymentMappers
{
    public static PaymentProviderCustomerResponse ToResponse(this PaymentProviderCustomer c)
        => new(c.Id, c.BillingAccountId, c.Provider, c.ProviderCustomerId,
               c.Email, c.Name, c.CreatedAtUtc, c.UpdatedAtUtc);

    public static PaymentMethodReferenceResponse ToResponse(this PaymentMethodReference m)
        => new(m.Id, m.BillingAccountId, m.Provider, m.ProviderPaymentMethodId,
               m.ProviderCustomerId, m.Brand, m.Last4, m.ExpMonth, m.ExpYear,
               m.IsDefault, m.CreatedAtUtc, m.UpdatedAtUtc);

    public static PaymentProviderEventLogResponse ToResponse(this PaymentProviderEventLog e)
        => new(e.Id, e.Provider, e.ProviderEventId, e.EventType,
               e.ProcessingStatus, e.ErrorMessage, e.ProcessedAtUtc, e.CreatedAtUtc);
}
