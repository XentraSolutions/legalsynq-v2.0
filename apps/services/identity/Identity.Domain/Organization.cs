namespace Identity.Domain;

public class Organization
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string? DisplayName { get; private set; }
    public string OrgType { get; private set; } = string.Empty;

    // Platform Phase 1: typed org-type FK (nullable during migration window; backfilled from OrgType)
    public Guid? OrganizationTypeId { get; private set; }

    public bool IsActive { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }
    public Guid? CreatedByUserId { get; private set; }
    public Guid? UpdatedByUserId { get; private set; }

    public Tenant Tenant { get; private set; } = null!;
    public OrganizationType? OrganizationTypeRef { get; private set; }
    public ICollection<OrganizationDomain> Domains { get; private set; } = [];
    public ICollection<OrganizationProduct> OrganizationProducts { get; private set; } = [];
    public ICollection<UserOrganizationMembership> Memberships { get; private set; } = [];
    public ICollection<UserRoleAssignment> RoleAssignments { get; private set; } = [];
    public ICollection<OrganizationRelationship> OutgoingRelationships { get; private set; } = [];
    public ICollection<OrganizationRelationship> IncomingRelationships { get; private set; } = [];

    private Organization() { }

    /// <summary>
    /// Legacy create: accepts OrgType string only (backward compatible).
    /// OrgType must be valid per the static OrgType class.
    /// Callers should prefer the overload that also supplies organizationTypeId.
    /// </summary>
    public static Organization Create(
        Guid tenantId,
        string name,
        string orgType,
        string? displayName = null,
        Guid? createdByUserId = null)
        => Create(tenantId, name, orgType, organizationTypeId: null, displayName, createdByUserId);

    /// <summary>
    /// Canonical create: accepts both the string OrgType (for backward compat / JWT claims)
    /// and the new OrganizationTypeId FK (Phase 1).
    /// When organizationTypeId is supplied the string OrgType should match the catalog record.
    /// When only orgType is supplied, OrganizationTypeId is left null until a backfill resolves it.
    /// </summary>
    public static Organization Create(
        Guid   tenantId,
        string name,
        string orgType,
        Guid?  organizationTypeId,
        string? displayName      = null,
        Guid?  createdByUserId   = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(orgType);

        if (!Identity.Domain.OrgType.IsValid(orgType))
            throw new ArgumentException($"Invalid OrgType: {orgType}", nameof(orgType));

        var now = DateTime.UtcNow;
        return new Organization
        {
            Id                 = Guid.NewGuid(),
            TenantId           = tenantId,
            Name               = name.Trim(),
            DisplayName        = displayName?.Trim(),
            OrgType            = orgType,
            OrganizationTypeId = organizationTypeId,
            IsActive           = true,
            CreatedAtUtc       = now,
            UpdatedAtUtc       = now,
            CreatedByUserId    = createdByUserId,
            UpdatedByUserId    = createdByUserId
        };
    }

    /// <summary>
    /// Phase A: assign the canonical OrganizationTypeId after creation or during backfill.
    /// The orgTypeCode should match OrgType string already stored on this entity for consistency.
    /// </summary>
    public void AssignOrganizationType(Guid organizationTypeId, string orgTypeCode)
    {
        OrganizationTypeId = organizationTypeId;
        // Keep OrgType string in sync so JWT claims remain backward-compatible.
        if (!string.IsNullOrWhiteSpace(orgTypeCode))
            OrgType = orgTypeCode;
        UpdatedAtUtc = DateTime.UtcNow;
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
