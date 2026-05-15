using Commerce.Domain.Common;

namespace Commerce.Domain.Billing;

public sealed class BillingProfile : Entity<Guid>
{
    public Guid BillingAccountId { get; private set; }
    public string? AddressLine1 { get; private set; }
    public string? AddressLine2 { get; private set; }
    public string? City { get; private set; }
    public string? StateRegion { get; private set; }
    public string? PostalCode { get; private set; }
    public string? Country { get; private set; }
    public string? TaxId { get; private set; }
    public bool TaxExempt { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    private BillingProfile() { }

    public static BillingProfile CreateEmpty(Guid billingAccountId, DateTime nowUtc)
    {
        return new BillingProfile
        {
            Id = Guid.NewGuid(),
            BillingAccountId = billingAccountId,
            TaxExempt = false,
            CreatedAtUtc = nowUtc,
            UpdatedAtUtc = nowUtc
        };
    }

    public void Update(
        string? addressLine1,
        string? addressLine2,
        string? city,
        string? stateRegion,
        string? postalCode,
        string? country,
        string? taxId,
        bool taxExempt,
        DateTime nowUtc)
    {
        AddressLine1 = string.IsNullOrWhiteSpace(addressLine1) ? null : addressLine1.Trim();
        AddressLine2 = string.IsNullOrWhiteSpace(addressLine2) ? null : addressLine2.Trim();
        City = string.IsNullOrWhiteSpace(city) ? null : city.Trim();
        StateRegion = string.IsNullOrWhiteSpace(stateRegion) ? null : stateRegion.Trim();
        PostalCode = string.IsNullOrWhiteSpace(postalCode) ? null : postalCode.Trim();
        Country = string.IsNullOrWhiteSpace(country) ? null : country.Trim().ToUpperInvariant();
        TaxId = string.IsNullOrWhiteSpace(taxId) ? null : taxId.Trim();
        TaxExempt = taxExempt;
        UpdatedAtUtc = nowUtc;
    }
}
