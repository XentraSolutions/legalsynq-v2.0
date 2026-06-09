using Commerce.Domain.Common;

namespace Commerce.Domain.Catalog;

public sealed class BundleItem : Entity<Guid>
{
    public Guid BundleId { get; private set; }
    public Guid? ProductId { get; private set; }
    public Guid? PlanId { get; private set; }
    public Guid? AddonId { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    private BundleItem() { }

    public static BundleItem Create(Guid bundleId, Guid? productId, Guid? planId, Guid? addonId, DateTime nowUtc)
    {
        var refCount =
            (productId.HasValue ? 1 : 0) +
            (planId.HasValue ? 1 : 0) +
            (addonId.HasValue ? 1 : 0);

        if (refCount != 1)
            throw new InvalidOperationException("BundleItem must reference exactly one of ProductId, PlanId, or AddonId.");

        return new BundleItem
        {
            Id = Guid.CreateVersion7(),
            BundleId = bundleId,
            ProductId = productId,
            PlanId = planId,
            AddonId = addonId,
            CreatedAtUtc = nowUtc
        };
    }
}
