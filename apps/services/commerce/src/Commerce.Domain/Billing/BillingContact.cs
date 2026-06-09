using Commerce.Domain.Billing.Enums;
using Commerce.Domain.Common;

namespace Commerce.Domain.Billing;

public sealed class BillingContact : Entity<Guid>
{
    public Guid BillingAccountId { get; private set; }
    public BillingContactType ContactType { get; private set; }
    public string Name { get; private set; } = default!;
    public string Email { get; private set; } = default!;
    public string? Phone { get; private set; }
    public bool IsPrimary { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    private BillingContact() { }

    public static BillingContact Create(
        Guid billingAccountId,
        BillingContactType contactType,
        string name,
        string email,
        string? phone,
        bool isPrimary,
        DateTime nowUtc)
    {
        return new BillingContact
        {
            Id = Guid.CreateVersion7(),
            BillingAccountId = billingAccountId,
            ContactType = contactType,
            Name = name.Trim(),
            Email = email.Trim(),
            Phone = string.IsNullOrWhiteSpace(phone) ? null : phone.Trim(),
            IsPrimary = isPrimary,
            CreatedAtUtc = nowUtc,
            UpdatedAtUtc = nowUtc
        };
    }

    public void Update(BillingContactType contactType, string name, string email, string? phone, DateTime nowUtc)
    {
        ContactType = contactType;
        Name = name.Trim();
        Email = email.Trim();
        Phone = string.IsNullOrWhiteSpace(phone) ? null : phone.Trim();
        UpdatedAtUtc = nowUtc;
    }

    public void SetPrimary(bool isPrimary, DateTime nowUtc)
    {
        if (IsPrimary == isPrimary) return;
        IsPrimary = isPrimary;
        UpdatedAtUtc = nowUtc;
    }
}
