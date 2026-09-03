namespace Identity.Domain;

public sealed class SynqLienUserAccessRoleAssignment
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid OrganizationId { get; private set; }
    public Guid UserId { get; private set; }
    public Guid RoleId { get; private set; }
    public bool IsActive { get; private set; }
    public Guid? ActiveSlot { get; private set; }
    public DateTime AssignedAtUtc { get; private set; }
    public DateTime? RemovedAtUtc { get; private set; }
    public Guid? AssignedByUserId { get; private set; }
    public Guid? RemovedByUserId { get; private set; }

    public SynqLienAccessRole Role { get; private set; } = null!;
    public User User { get; private set; } = null!;

    private SynqLienUserAccessRoleAssignment() { }

    public static SynqLienUserAccessRoleAssignment Create(
        Guid tenantId, Guid organizationId, Guid userId, Guid roleId, Guid? actorUserId) =>
        new()
        {
            Id = Guid.CreateVersion7(),
            TenantId = tenantId,
            OrganizationId = organizationId,
            UserId = userId,
            RoleId = roleId,
            IsActive = true,
            ActiveSlot = Guid.Empty,
            AssignedAtUtc = DateTime.UtcNow,
            AssignedByUserId = actorUserId,
        };

    public void Remove(Guid? actorUserId)
    {
        IsActive = false;
        ActiveSlot = null;
        RemovedAtUtc = DateTime.UtcNow;
        RemovedByUserId = actorUserId;
    }
}
