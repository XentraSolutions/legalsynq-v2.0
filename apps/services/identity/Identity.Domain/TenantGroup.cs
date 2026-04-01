namespace Identity.Domain;

/// <summary>
/// A tenant-scoped group that bundles users for permission and team-membership purposes.
/// Groups are managed by TenantAdmin and PlatformAdmin via the Control Center.
/// </summary>
public class TenantGroup
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }
    public Guid? CreatedByUserId { get; private set; }

    public Tenant Tenant { get; private set; } = null!;
    public ICollection<GroupMembership> Members { get; private set; } = [];

    private TenantGroup() { }

    public static TenantGroup Create(
        Guid tenantId,
        string name,
        string? description = null,
        Guid? createdByUserId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var now = DateTime.UtcNow;
        return new TenantGroup
        {
            Id              = Guid.NewGuid(),
            TenantId        = tenantId,
            Name            = name.Trim(),
            Description     = description?.Trim(),
            IsActive        = true,
            CreatedAtUtc    = now,
            UpdatedAtUtc    = now,
            CreatedByUserId = createdByUserId,
        };
    }

    public void Update(string name, string? description)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name         = name.Trim();
        Description  = description?.Trim();
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void Deactivate()
    {
        IsActive     = false;
        UpdatedAtUtc = DateTime.UtcNow;
    }
}
