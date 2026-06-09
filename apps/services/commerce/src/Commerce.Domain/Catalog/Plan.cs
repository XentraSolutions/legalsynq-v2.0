using Commerce.Domain.Catalog.Enums;
using Commerce.Domain.Common;

namespace Commerce.Domain.Catalog;

public sealed class Plan : Entity<Guid>
{
    public Guid? ProductId { get; private set; }
    public string Key { get; private set; } = default!;
    public string Name { get; private set; } = default!;
    public string? Description { get; private set; }
    public CatalogStatus Status { get; private set; }
    public BillingInterval BillingInterval { get; private set; }
    public int? TrialDays { get; private set; }
    public int SortOrder { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    private Plan() { }

    public static Plan Create(
        Guid? productId,
        string key,
        string name,
        string? description,
        BillingInterval interval,
        int? trialDays,
        int sortOrder,
        DateTime nowUtc)
    {
        return new Plan
        {
            Id = Guid.CreateVersion7(),
            ProductId = productId,
            Key = CatalogKey.Normalize(key),
            Name = name.Trim(),
            Description = description?.Trim(),
            Status = CatalogStatus.Draft,
            BillingInterval = interval,
            TrialDays = trialDays,
            SortOrder = sortOrder,
            CreatedAtUtc = nowUtc,
            UpdatedAtUtc = nowUtc
        };
    }

    /// <summary>
    /// Updates plan metadata. When the plan is retired only Name/Description
    /// are mutable per the spec ("Retired plans cannot be changed except
    /// metadata if necessary").
    /// </summary>
    public void Update(
        string name,
        string? description,
        BillingInterval interval,
        int? trialDays,
        int sortOrder,
        DateTime nowUtc)
    {
        if (Status == CatalogStatus.Retired)
        {
            // Only metadata changes allowed
            Name = name.Trim();
            Description = description?.Trim();
            UpdatedAtUtc = nowUtc;
            return;
        }

        Name = name.Trim();
        Description = description?.Trim();
        BillingInterval = interval;
        TrialDays = trialDays;
        SortOrder = sortOrder;
        UpdatedAtUtc = nowUtc;
    }

    public void Activate(DateTime nowUtc)
    {
        if (Status == CatalogStatus.Retired)
            throw new InvalidOperationException("Retired plan cannot be activated.");
        Status = CatalogStatus.Active;
        UpdatedAtUtc = nowUtc;
    }

    public void Retire(DateTime nowUtc)
    {
        Status = CatalogStatus.Retired;
        UpdatedAtUtc = nowUtc;
    }
}
