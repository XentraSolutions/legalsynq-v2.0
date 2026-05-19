using Commerce.Domain.Catalog.Enums;
using Commerce.Domain.Common;

namespace Commerce.Domain.Catalog;

public sealed class Addon : Entity<Guid>
{
    public Guid? ProductId { get; private set; }
    public string Key { get; private set; } = default!;
    public string Name { get; private set; } = default!;
    public string? Description { get; private set; }
    public CatalogStatus Status { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    private Addon() { }

    public static Addon Create(Guid? productId, string key, string name, string? description, DateTime nowUtc)
    {
        return new Addon
        {
            Id = Guid.CreateVersion7(),
            ProductId = productId,
            Key = CatalogKey.Normalize(key),
            Name = name.Trim(),
            Description = description?.Trim(),
            Status = CatalogStatus.Draft,
            CreatedAtUtc = nowUtc,
            UpdatedAtUtc = nowUtc
        };
    }

    public void Update(string name, string? description, DateTime nowUtc)
    {
        Name = name.Trim();
        Description = description?.Trim();
        UpdatedAtUtc = nowUtc;
    }

    public void Activate(DateTime nowUtc)
    {
        if (Status == CatalogStatus.Retired)
            throw new InvalidOperationException("Retired add-on cannot be activated.");
        Status = CatalogStatus.Active;
        UpdatedAtUtc = nowUtc;
    }

    public void Retire(DateTime nowUtc)
    {
        Status = CatalogStatus.Retired;
        UpdatedAtUtc = nowUtc;
    }
}
