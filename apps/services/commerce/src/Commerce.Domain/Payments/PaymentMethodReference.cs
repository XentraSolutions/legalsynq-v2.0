using Commerce.Domain.Common;
using Commerce.Domain.Payments.Enums;

namespace Commerce.Domain.Payments;

/// <summary>
/// Stores SAFE provider-issued payment method references. Card numbers,
/// CVCs, IBANs and other sensitive details MUST NOT be stored here —
/// only the brand, last 4 digits and expiry, exactly the fields a
/// provider returns in its public webhook events.
/// </summary>
public sealed class PaymentMethodReference : Entity<Guid>
{
    public Guid BillingAccountId { get; private set; }
    public PaymentProviderType Provider { get; private set; }
    public string ProviderPaymentMethodId { get; private set; } = default!;
    public string? ProviderCustomerId { get; private set; }
    public string? Brand { get; private set; }
    public string? Last4 { get; private set; }
    public int? ExpMonth { get; private set; }
    public int? ExpYear { get; private set; }
    public bool IsDefault { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    private PaymentMethodReference() { }

    public static PaymentMethodReference Create(
        Guid billingAccountId,
        PaymentProviderType provider,
        string providerPaymentMethodId,
        string? providerCustomerId,
        string? brand,
        string? last4,
        int? expMonth,
        int? expYear,
        DateTime nowUtc)
    {
        if (billingAccountId == Guid.Empty)
            throw new InvalidOperationException("BillingAccountId is required.");
        if (string.IsNullOrWhiteSpace(providerPaymentMethodId))
            throw new InvalidOperationException("ProviderPaymentMethodId is required.");
        if (last4 is { Length: > 4 })
            throw new InvalidOperationException("Last4 must be 4 characters or fewer.");
        if (expMonth is < 1 or > 12)
            throw new InvalidOperationException("ExpMonth must be between 1 and 12.");
        if (expYear is < 2000 or > 2100)
            throw new InvalidOperationException("ExpYear must be a plausible 4-digit year.");

        return new PaymentMethodReference
        {
            Id = Guid.CreateVersion7(),
            BillingAccountId = billingAccountId,
            Provider = provider,
            ProviderPaymentMethodId = providerPaymentMethodId.Trim(),
            ProviderCustomerId = string.IsNullOrWhiteSpace(providerCustomerId) ? null : providerCustomerId.Trim(),
            Brand = string.IsNullOrWhiteSpace(brand) ? null : brand.Trim(),
            Last4 = string.IsNullOrWhiteSpace(last4) ? null : last4.Trim(),
            ExpMonth = expMonth,
            ExpYear = expYear,
            IsDefault = false,
            CreatedAtUtc = nowUtc,
            UpdatedAtUtc = nowUtc
        };
    }

    public void UpdateFromProvider(
        string? providerCustomerId, string? brand, string? last4,
        int? expMonth, int? expYear, DateTime nowUtc)
    {
        ProviderCustomerId = string.IsNullOrWhiteSpace(providerCustomerId) ? ProviderCustomerId : providerCustomerId.Trim();
        if (!string.IsNullOrWhiteSpace(brand)) Brand = brand.Trim();
        if (!string.IsNullOrWhiteSpace(last4))
        {
            if (last4.Length > 4) throw new InvalidOperationException("Last4 must be 4 characters or fewer.");
            Last4 = last4.Trim();
        }
        if (expMonth.HasValue) ExpMonth = expMonth;
        if (expYear.HasValue) ExpYear = expYear;
        UpdatedAtUtc = nowUtc;
    }

    public void MakeDefault(DateTime nowUtc)
    {
        IsDefault = true;
        UpdatedAtUtc = nowUtc;
    }

    public void DemoteDefault(DateTime nowUtc)
    {
        if (!IsDefault) return;
        IsDefault = false;
        UpdatedAtUtc = nowUtc;
    }
}
