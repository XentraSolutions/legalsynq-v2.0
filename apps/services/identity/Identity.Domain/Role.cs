namespace Identity.Domain;

public class Role
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public bool IsSystemRole { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    public Tenant Tenant { get; private set; } = null!;

    private Role() { }

    public static Role Create(
        Guid tenantId,
        string name,
        string? description = null,
        bool isSystemRole = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var now = DateTime.UtcNow;
        return new Role
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = name.Trim(),
            Description = description?.Trim(),
            IsSystemRole = isSystemRole,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
    }
}
