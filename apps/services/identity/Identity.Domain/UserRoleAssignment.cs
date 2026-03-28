namespace Identity.Domain;

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
