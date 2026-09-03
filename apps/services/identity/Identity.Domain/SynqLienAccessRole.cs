namespace Identity.Domain;

/// <summary>
/// Organization-scoped SynqLien management role. This is intentionally separate
/// from commercial personas such as SYNQLIEN_SELLER and SYNQLIEN_BUYER.
/// </summary>
public sealed class SynqLienAccessRole
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid OrganizationId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public bool IsSystem { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }
    public Guid? CreatedByUserId { get; private set; }
    public Guid? UpdatedByUserId { get; private set; }

    public ICollection<SynqLienAccessRolePermission> Permissions { get; private set; } = [];
    public ICollection<SynqLienUserAccessRoleAssignment> Assignments { get; private set; } = [];

    private SynqLienAccessRole() { }

    public static SynqLienAccessRole Create(
        Guid tenantId,
        Guid organizationId,
        string name,
        string? description,
        bool isSystem,
        Guid? actorUserId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var now = DateTime.UtcNow;
        return new SynqLienAccessRole
        {
            Id = Guid.CreateVersion7(),
            TenantId = tenantId,
            OrganizationId = organizationId,
            Name = name.Trim(),
            Description = Normalize(description),
            IsSystem = isSystem,
            IsActive = true,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
            CreatedByUserId = actorUserId,
            UpdatedByUserId = actorUserId,
        };
    }

    public void Update(string name, string? description, Guid? actorUserId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name.Trim();
        Description = Normalize(description);
        UpdatedAtUtc = DateTime.UtcNow;
        UpdatedByUserId = actorUserId;
    }

    public void Deactivate(Guid? actorUserId)
    {
        IsActive = false;
        UpdatedAtUtc = DateTime.UtcNow;
        UpdatedByUserId = actorUserId;
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
