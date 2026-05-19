using Commerce.Domain.Common;
using Commerce.Domain.Payments.Enums;

namespace Commerce.Domain.Payments;

/// <summary>
/// Maps a Commerce <c>BillingAccount</c> to a payment-provider customer.
/// One row per (BillingAccount, Provider). Holds only safe references —
/// never card data.
/// </summary>
public sealed class PaymentProviderCustomer : Entity<Guid>
{
    public Guid BillingAccountId { get; private set; }
    public PaymentProviderType Provider { get; private set; }
    public string ProviderCustomerId { get; private set; } = default!;
    public string? Email { get; private set; }
    public string? Name { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    private PaymentProviderCustomer() { }

    public static PaymentProviderCustomer Create(
        Guid billingAccountId,
        PaymentProviderType provider,
        string providerCustomerId,
        string? email,
        string? name,
        DateTime nowUtc)
    {
        if (billingAccountId == Guid.Empty)
            throw new InvalidOperationException("BillingAccountId is required.");
        if (string.IsNullOrWhiteSpace(providerCustomerId))
            throw new InvalidOperationException("ProviderCustomerId is required.");

        return new PaymentProviderCustomer
        {
            Id = Guid.CreateVersion7(),
            BillingAccountId = billingAccountId,
            Provider = provider,
            ProviderCustomerId = providerCustomerId.Trim(),
            Email = string.IsNullOrWhiteSpace(email) ? null : email.Trim(),
            Name = string.IsNullOrWhiteSpace(name) ? null : name.Trim(),
            CreatedAtUtc = nowUtc,
            UpdatedAtUtc = nowUtc
        };
    }

    public void UpdateContact(string? email, string? name, DateTime nowUtc)
    {
        Email = string.IsNullOrWhiteSpace(email) ? null : email.Trim();
        Name = string.IsNullOrWhiteSpace(name) ? null : name.Trim();
        UpdatedAtUtc = nowUtc;
    }
}
