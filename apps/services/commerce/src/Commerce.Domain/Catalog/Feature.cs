using Commerce.Domain.Catalog.Enums;
using Commerce.Domain.Common;

namespace Commerce.Domain.Catalog;

public sealed class Feature : Entity<Guid>
{
    public Guid ProductId { get; private set; }
    public string Key { get; private set; } = default!;
    public string Name { get; private set; } = default!;
    public string? Description { get; private set; }
    public FeatureType FeatureType { get; private set; }
    public CatalogStatus Status { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    private Feature() { }

    public static Feature Create(Guid productId, string key, string name, string? description, FeatureType type, DateTime nowUtc)
    {
        return new Feature
        {
            Id = Guid.NewGuid(),
            ProductId = productId,
            Key = CatalogKey.Normalize(key),
            Name = name.Trim(),
            Description = description?.Trim(),
            FeatureType = type,
            Status = CatalogStatus.Draft,
            CreatedAtUtc = nowUtc,
            UpdatedAtUtc = nowUtc
        };
    }

    public void Update(string name, string? description, FeatureType type, DateTime nowUtc)
    {
        Name = name.Trim();
        Description = description?.Trim();
        FeatureType = type;
        UpdatedAtUtc = nowUtc;
    }

    public void Activate(DateTime nowUtc)
    {
        if (Status == CatalogStatus.Retired)
            throw new InvalidOperationException("Retired feature cannot be activated.");
        Status = CatalogStatus.Active;
        UpdatedAtUtc = nowUtc;
    }

    public void Retire(DateTime nowUtc)
    {
        Status = CatalogStatus.Retired;
        UpdatedAtUtc = nowUtc;
    }
}
