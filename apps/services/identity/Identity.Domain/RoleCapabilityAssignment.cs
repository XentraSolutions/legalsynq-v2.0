namespace Identity.Domain;

/// <summary>
/// Junction entity that assigns a <see cref="Capability"/> to a tenant-level <see cref="Role"/>.
/// Assignments are additive: a user's effective permissions are the union of all capabilities
/// across every active role they hold.
///
/// Hard-deleting a row revokes the permission. No soft-delete here — audit trail lives in
/// <see cref="AuditLog"/> via the admin endpoint.
/// </summary>
public class RoleCapabilityAssignment
{
    public Guid RoleId         { get; private set; }
    public Guid CapabilityId   { get; private set; }
    public DateTime AssignedAtUtc { get; private set; }
    public Guid? AssignedByUserId { get; private set; }

    public Role       Role       { get; private set; } = null!;
    public Capability Capability { get; private set; } = null!;

    private RoleCapabilityAssignment() { }

    public static RoleCapabilityAssignment Create(
        Guid roleId,
        Guid capabilityId,
        Guid? assignedByUserId = null)
    {
        return new RoleCapabilityAssignment
        {
            RoleId           = roleId,
            CapabilityId     = capabilityId,
            AssignedAtUtc    = DateTime.UtcNow,
            AssignedByUserId = assignedByUserId,
        };
    }
}
