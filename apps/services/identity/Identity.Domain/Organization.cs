namespace Identity.Domain;

public class Organization
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string? DisplayName { get; private set; }
    public string OrgType { get; private set; } = string.Empty;
    public bool IsActive { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }
    public Guid? CreatedByUserId { get; private set; }
    public Guid? UpdatedByUserId { get; private set; }

    public Tenant Tenant { get; private set; } = null!;
    public ICollection<OrganizationDomain> Domains { get; private set; } = [];
    public ICollection<OrganizationProduct> OrganizationProducts { get; private set; } = [];
    public ICollection<UserOrganizationMembership> Memberships { get; private set; } = [];
    public ICollection<UserRoleAssignment> RoleAssignments { get; private set; } = [];

    private Organization() { }

    public static Organization Create(
        Guid tenantId,
        string name,
        string orgType,
        string? displayName = null,
        Guid? createdByUserId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(orgType);

        if (!Identity.Domain.OrgType.IsValid(orgType))
            throw new ArgumentException($"Invalid OrgType: {orgType}", nameof(orgType));

        var now = DateTime.UtcNow;
        return new Organization
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = name.Trim(),
            DisplayName = displayName?.Trim(),
            OrgType = orgType,
            IsActive = true,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
            CreatedByUserId = createdByUserId,
            UpdatedByUserId = createdByUserId
        };
    }

    public void Update(string name, string? displayName, Guid? updatedByUserId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name.Trim();
        DisplayName = displayName?.Trim();
        UpdatedByUserId = updatedByUserId;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void Deactivate(Guid? updatedByUserId)
    {
        IsActive = false;
        UpdatedByUserId = updatedByUserId;
        UpdatedAtUtc = DateTime.UtcNow;
    }
}
