namespace Identity.Domain;

/// <summary>
/// TODO [LEGACY — Phase F]: this table predates ScopedRoleAssignment (Phase 4).
/// ScopedRoleAssignment is the forward-looking model (with scope discriminator).
/// UserRoleAssignment is kept for backward compatibility; do not create new records here.
/// All rows have been back-populated into ScopedRoleAssignments via migration 20260330110004.
/// </summary>
public class UserRoleAssignment
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public Guid RoleId { get; private set; }
    public Guid? OrganizationId { get; private set; }
    public DateTime AssignedAtUtc { get; private set; }
    public Guid? AssignedByUserId { get; private set; }

    public User User { get; private set; } = null!;
    public Role Role { get; private set; } = null!;
    public Organization? Organization { get; private set; }

    private UserRoleAssignment() { }

    public static UserRoleAssignment Create(
        Guid userId,
        Guid roleId,
        Guid? organizationId = null,
        Guid? assignedByUserId = null)
    {
        return new UserRoleAssignment
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            RoleId = roleId,
            OrganizationId = organizationId,
            AssignedAtUtc = DateTime.UtcNow,
            AssignedByUserId = assignedByUserId
        };
    }
}
