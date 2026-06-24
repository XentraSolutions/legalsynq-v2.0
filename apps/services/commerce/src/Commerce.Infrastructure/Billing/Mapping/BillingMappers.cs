using Commerce.Contracts.Billing;
using Commerce.Domain.Billing;

namespace Commerce.Infrastructure.Billing.Mapping;

internal static class BillingMappers
{
    public static BillingAccountResponse ToResponse(this BillingAccount e) => new(
        e.Id, e.AccountNumber, e.DisplayName, e.LegalName, e.Status,
        e.DefaultCurrency, e.CreatedAtUtc, e.UpdatedAtUtc);

    public static ExternalRefResponse ToResponse(this BillingAccountExternalRef e) => new(
        e.Id, e.BillingAccountId, e.HostPlatformKey, e.ExternalTenantId,
        e.ExternalCustomerRef, e.IsPrimary, e.CreatedAtUtc, e.UpdatedAtUtc);

    public static BillingContactResponse ToResponse(this BillingContact e) => new(
        e.Id, e.BillingAccountId, e.ContactType, e.Name, e.Email, e.Phone,
        e.IsPrimary, e.CreatedAtUtc, e.UpdatedAtUtc);

    public static BillingProfileResponse ToResponse(this BillingProfile e) => new(
        e.Id, e.BillingAccountId, e.AddressLine1, e.AddressLine2, e.City,
        e.StateRegion, e.PostalCode, e.Country, e.TaxId, e.TaxExempt,
        e.CreatedAtUtc, e.UpdatedAtUtc);

    public static BillingAccountAuditEventResponse ToResponse(this BillingAccountAuditEvent e) => new(
        e.Id, e.BillingAccountId, e.EventType, e.Description, e.ActorType,
        e.ActorId, e.MetadataJson, e.CreatedAtUtc);
}
