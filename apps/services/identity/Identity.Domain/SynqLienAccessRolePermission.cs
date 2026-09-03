namespace Identity.Domain;

public sealed class SynqLienAccessRolePermission
{
    public Guid RoleId { get; private set; }
    public Guid PermissionId { get; private set; }

    public SynqLienAccessRole Role { get; private set; } = null!;
    public Permission Permission { get; private set; } = null!;

    private SynqLienAccessRolePermission() { }

    public static SynqLienAccessRolePermission Create(Guid roleId, Guid permissionId) =>
        new() { RoleId = roleId, PermissionId = permissionId };
}
