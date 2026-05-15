using Commerce.Domain.Catalog.Enums;
using Commerce.Domain.Common;

namespace Commerce.Domain.Catalog;

public sealed class Product : Entity<Guid>
{
    public string Key { get; private set; } = default!;
    public string Name { get; private set; } = default!;
    public string? Description { get; private set; }
    public CatalogStatus Status { get; private set; }
    public int SortOrder { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    private Product() { }

    public static Product Create(string key, string name, string? description, int sortOrder, DateTime nowUtc)
    {
        return new Product
        {
            Id = Guid.NewGuid(),
            Key = CatalogKey.Normalize(key),
            Name = name.Trim(),
            Description = description?.Trim(),
            Status = CatalogStatus.Draft,
            SortOrder = sortOrder,
            CreatedAtUtc = nowUtc,
            UpdatedAtUtc = nowUtc
        };
    }

    public void Update(string name, string? description, int sortOrder, DateTime nowUtc)
    {
        Name = name.Trim();
        Description = description?.Trim();
        SortOrder = sortOrder;
        UpdatedAtUtc = nowUtc;
    }

    public void Activate(DateTime nowUtc)
    {
        if (Status == CatalogStatus.Retired)
            throw new InvalidOperationException("Retired product cannot be activated.");
        Status = CatalogStatus.Active;
        UpdatedAtUtc = nowUtc;
    }

    public void Retire(DateTime nowUtc)
    {
        Status = CatalogStatus.Retired;
        UpdatedAtUtc = nowUtc;
    }
}
