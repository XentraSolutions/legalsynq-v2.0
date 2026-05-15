using Commerce.Domain.Common;

namespace Commerce.Domain.Billing;

public sealed class BillingAccountExternalRef : Entity<Guid>
{
    public Guid BillingAccountId { get; private set; }
    public string HostPlatformKey { get; private set; } = default!;
    public string ExternalTenantId { get; private set; } = default!;
    public string? ExternalCustomerRef { get; private set; }
    public bool IsPrimary { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    private BillingAccountExternalRef() { }

    public static BillingAccountExternalRef Create(
        Guid billingAccountId,
        string hostPlatformKey,
        string externalTenantId,
        string? externalCustomerRef,
        bool isPrimary,
        DateTime nowUtc)
    {
        return new BillingAccountExternalRef
        {
            Id = Guid.NewGuid(),
            BillingAccountId = billingAccountId,
            HostPlatformKey = Billing.HostPlatformKey.Normalize(hostPlatformKey),
            ExternalTenantId = externalTenantId.Trim(),
            ExternalCustomerRef = string.IsNullOrWhiteSpace(externalCustomerRef) ? null : externalCustomerRef.Trim(),
            IsPrimary = isPrimary,
            CreatedAtUtc = nowUtc,
            UpdatedAtUtc = nowUtc
        };
    }

    public void Update(string hostPlatformKey, string externalTenantId, string? externalCustomerRef, DateTime nowUtc)
    {
        HostPlatformKey = Billing.HostPlatformKey.Normalize(hostPlatformKey);
        ExternalTenantId = externalTenantId.Trim();
        ExternalCustomerRef = string.IsNullOrWhiteSpace(externalCustomerRef) ? null : externalCustomerRef.Trim();
        UpdatedAtUtc = nowUtc;
    }

    public void SetPrimary(bool isPrimary, DateTime nowUtc)
    {
        if (IsPrimary == isPrimary) return;
        IsPrimary = isPrimary;
        UpdatedAtUtc = nowUtc;
    }
}
