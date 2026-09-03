namespace Identity.Domain;

/// <summary>
/// Multi-tenant account linking join table.
/// Maps one Identity user account (one email) to multiple CareConnect tenants.
/// Created when a user self-enrolls on a second tenant or is invited to one.
/// </summary>
public class UserTenant
{
    public Guid     Id           { get; private set; }
    public Guid     UserId       { get; private set; }
    public Guid     TenantId     { get; private set; }
    public bool     IsActive     { get; private set; }
    public DateTime JoinedAtUtc  { get; private set; }

    public User   User   { get; private set; } = null!;
    public Tenant Tenant { get; private set; } = null!;

    private UserTenant() { }

    public void Activate() => IsActive = true;

    public static UserTenant Create(Guid userId, Guid tenantId)
    {
        if (userId   == Guid.Empty) throw new ArgumentException("UserId is required.",   nameof(userId));
        if (tenantId == Guid.Empty) throw new ArgumentException("TenantId is required.", nameof(tenantId));

        return new UserTenant
        {
            Id          = Guid.CreateVersion7(),
            UserId      = userId,
            TenantId    = tenantId,
            IsActive    = true,
            JoinedAtUtc = DateTime.UtcNow,
        };
    }
}
