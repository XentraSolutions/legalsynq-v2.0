using Commerce.Domain.Common;

namespace Commerce.Domain.Catalog;

public sealed class PlanFeature : Entity<Guid>
{
    public Guid PlanId { get; private set; }
    public Guid FeatureId { get; private set; }
    public bool IsEnabled { get; private set; }
    public long? LimitValue { get; private set; }
    public long? MeteredIncludedUnits { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    private PlanFeature() { }

    public static PlanFeature Create(
        Guid planId,
        Guid featureId,
        bool isEnabled,
        long? limitValue,
        long? meteredIncludedUnits,
        DateTime nowUtc)
    {
        return new PlanFeature
        {
            Id = Guid.CreateVersion7(),
            PlanId = planId,
            FeatureId = featureId,
            IsEnabled = isEnabled,
            LimitValue = limitValue,
            MeteredIncludedUnits = meteredIncludedUnits,
            CreatedAtUtc = nowUtc,
            UpdatedAtUtc = nowUtc
        };
    }
}
