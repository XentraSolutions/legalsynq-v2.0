using Commerce.Domain.Catalog.Enums;
using Commerce.Domain.Common;

namespace Commerce.Domain.Catalog;

public sealed class Price : Entity<Guid>
{
    public Guid? PlanId { get; private set; }
    public Guid? AddonId { get; private set; }
    public Guid? BundleId { get; private set; }
    public string Currency { get; private set; } = default!;
    public long AmountMinor { get; private set; }
    public BillingInterval BillingInterval { get; private set; }
    public CatalogStatus Status { get; private set; }
    public DateTime EffectiveFromUtc { get; private set; }
    public DateTime? EffectiveToUtc { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    private Price() { }

    public static Price Create(
        Guid? planId,
        Guid? addonId,
        Guid? bundleId,
        string currency,
        long amountMinor,
        BillingInterval interval,
        DateTime effectiveFromUtc,
        DateTime? effectiveToUtc,
        DateTime nowUtc)
    {
        var refCount =
            (planId.HasValue ? 1 : 0) +
            (addonId.HasValue ? 1 : 0) +
            (bundleId.HasValue ? 1 : 0);

        if (refCount != 1)
            throw new InvalidOperationException("Price must reference exactly one of PlanId, AddonId, or BundleId.");

        if (amountMinor < 0)
            throw new InvalidOperationException("AmountMinor must be >= 0.");

        if (effectiveToUtc.HasValue && effectiveToUtc.Value <= effectiveFromUtc)
            throw new InvalidOperationException("EffectiveToUtc must be greater than EffectiveFromUtc.");

        return new Price
        {
            Id = Guid.CreateVersion7(),
            PlanId = planId,
            AddonId = addonId,
            BundleId = bundleId,
            Currency = currency.Trim().ToUpperInvariant(),
            AmountMinor = amountMinor,
            BillingInterval = interval,
            Status = CatalogStatus.Draft,
            EffectiveFromUtc = effectiveFromUtc,
            EffectiveToUtc = effectiveToUtc,
            CreatedAtUtc = nowUtc,
            UpdatedAtUtc = nowUtc
        };
    }

    public void Update(
        string currency,
        long amountMinor,
        BillingInterval interval,
        DateTime effectiveFromUtc,
        DateTime? effectiveToUtc,
        DateTime nowUtc)
    {
        if (amountMinor < 0)
            throw new InvalidOperationException("AmountMinor must be >= 0.");
        if (effectiveToUtc.HasValue && effectiveToUtc.Value <= effectiveFromUtc)
            throw new InvalidOperationException("EffectiveToUtc must be greater than EffectiveFromUtc.");

        Currency = currency.Trim().ToUpperInvariant();
        AmountMinor = amountMinor;
        BillingInterval = interval;
        EffectiveFromUtc = effectiveFromUtc;
        EffectiveToUtc = effectiveToUtc;
        UpdatedAtUtc = nowUtc;
    }

    public void Activate(DateTime nowUtc)
    {
        if (Status == CatalogStatus.Retired)
            throw new InvalidOperationException("Retired price cannot be activated.");
        Status = CatalogStatus.Active;
        UpdatedAtUtc = nowUtc;
    }

    public void Retire(DateTime nowUtc)
    {
        Status = CatalogStatus.Retired;
        UpdatedAtUtc = nowUtc;
    }

    public bool OverlapsWith(Price other)
    {
        var aEnd = EffectiveToUtc ?? DateTime.MaxValue;
        var bEnd = other.EffectiveToUtc ?? DateTime.MaxValue;
        return EffectiveFromUtc < bEnd && other.EffectiveFromUtc < aEnd;
    }
}
