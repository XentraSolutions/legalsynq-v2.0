namespace Identity.Domain;

public class RoleCapability
{
    public Guid ProductRoleId { get; private set; }
    public Guid CapabilityId { get; private set; }

    public ProductRole ProductRole { get; private set; } = null!;
    public Capability Capability { get; private set; } = null!;

    private RoleCapability() { }

    public static RoleCapability Create(Guid productRoleId, Guid capabilityId)
    {
        return new RoleCapability
        {
            ProductRoleId = productRoleId,
            CapabilityId = capabilityId
        };
    }
}
