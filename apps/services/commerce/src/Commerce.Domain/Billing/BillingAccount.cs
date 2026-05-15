using Commerce.Domain.Billing.Enums;
using Commerce.Domain.Common;

namespace Commerce.Domain.Billing;

public sealed class BillingAccount : Entity<Guid>
{
    public string AccountNumber { get; private set; } = default!;
    public string DisplayName { get; private set; } = default!;
    public string? LegalName { get; private set; }
    public BillingAccountStatus Status { get; private set; }
    public string DefaultCurrency { get; private set; } = default!;
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    private BillingAccount() { }

    public static BillingAccount Create(
        string accountNumber,
        string displayName,
        string? legalName,
        string defaultCurrency,
        DateTime nowUtc)
    {
        return new BillingAccount
        {
            Id = Guid.NewGuid(),
            AccountNumber = accountNumber.Trim(),
            DisplayName = displayName.Trim(),
            LegalName = string.IsNullOrWhiteSpace(legalName) ? null : legalName.Trim(),
            DefaultCurrency = defaultCurrency.Trim().ToUpperInvariant(),
            Status = BillingAccountStatus.Draft,
            CreatedAtUtc = nowUtc,
            UpdatedAtUtc = nowUtc
        };
    }

    public void Update(string displayName, string? legalName, string defaultCurrency, DateTime nowUtc)
    {
        if (Status == BillingAccountStatus.Closed)
            throw new InvalidOperationException("Closed billing account cannot be updated.");
        DisplayName = displayName.Trim();
        LegalName = string.IsNullOrWhiteSpace(legalName) ? null : legalName.Trim();
        DefaultCurrency = defaultCurrency.Trim().ToUpperInvariant();
        UpdatedAtUtc = nowUtc;
    }

    public void Activate(DateTime nowUtc)
    {
        if (Status == BillingAccountStatus.Closed)
            throw new InvalidOperationException("Closed billing account cannot be reactivated.");
        if (Status == BillingAccountStatus.Active) return;
        // Allowed: Draft → Active, Suspended → Active.
        Status = BillingAccountStatus.Active;
        UpdatedAtUtc = nowUtc;
    }

    public void Suspend(DateTime nowUtc)
    {
        if (Status == BillingAccountStatus.Closed)
            throw new InvalidOperationException("Closed billing account cannot be suspended.");
        if (Status != BillingAccountStatus.Active)
            throw new InvalidOperationException("Only active billing accounts can be suspended.");
        Status = BillingAccountStatus.Suspended;
        UpdatedAtUtc = nowUtc;
    }

    public void Close(DateTime nowUtc)
    {
        if (Status == BillingAccountStatus.Closed) return;
        // Allowed: Active → Closed, Suspended → Closed.
        // Disallowed: Draft → Closed (close a never-activated account through delete instead).
        if (Status != BillingAccountStatus.Active && Status != BillingAccountStatus.Suspended)
            throw new InvalidOperationException(
                $"Billing account in status '{Status}' cannot be closed; it must first be Active or Suspended.");
        Status = BillingAccountStatus.Closed;
        UpdatedAtUtc = nowUtc;
    }
}
