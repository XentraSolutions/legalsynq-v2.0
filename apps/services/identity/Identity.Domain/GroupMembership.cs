namespace Identity.Domain;

/// <summary>
/// [DEPRECATED — LS-COR-AUT-006A] Legacy group membership entity.
/// Replaced by <see cref="AccessGroupMembership"/> (LS-COR-AUT-004).
/// Retained only for EF migration compatibility — no active runtime code should reference this entity.
/// The GroupMemberships table can be dropped once all data has been migrated to AccessGroupMemberships.
/// </summary>
[Obsolete("Legacy entity — replaced by AccessGroupMembership. See LS-COR-AUT-006A.")]
public class GroupMembership
{
    public Guid Id { get; private set; }
    public Guid GroupId { get; private set; }
    public Guid UserId { get; private set; }
    public Guid TenantId { get; private set; }
    public DateTime JoinedAtUtc { get; private set; }
    public Guid? AddedByUserId { get; private set; }

    public TenantGroup Group { get; private set; } = null!;
    public User User { get; private set; } = null!;

    private GroupMembership() { }

    public static GroupMembership Create(
        Guid groupId,
        Guid userId,
        Guid tenantId,
        Guid? addedByUserId = null)
    {
        return new GroupMembership
        {
            Id            = Guid.NewGuid(),
            GroupId       = groupId,
            UserId        = userId,
            TenantId      = tenantId,
            JoinedAtUtc   = DateTime.UtcNow,
            AddedByUserId = addedByUserId,
        };
    }
}
