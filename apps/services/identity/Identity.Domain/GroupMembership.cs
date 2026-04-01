namespace Identity.Domain;

/// <summary>
/// Records that a user belongs to a TenantGroup.
/// Unique per (UserId, GroupId) — a user can only appear once per group.
/// </summary>
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
