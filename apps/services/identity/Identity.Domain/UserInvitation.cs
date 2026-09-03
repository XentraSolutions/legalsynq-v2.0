namespace Identity.Domain;

/// <summary>
/// Records a pending invitation for a user to join a tenant.
/// The raw token is emailed; only the SHA-256 hash is stored.
/// Status lifecycle: PENDING → ACCEPTED | EXPIRED | REVOKED.
/// </summary>
public class UserInvitation
{
    public static class Statuses
    {
        public const string Pending  = "PENDING";
        public const string Accepted = "ACCEPTED";
        public const string Expired  = "EXPIRED";
        public const string Revoked  = "REVOKED";
    }

    public static class PortalOrigins
    {
        public const string TenantPortal   = "TENANT_PORTAL";
        public const string ControlCenter  = "CONTROL_CENTER";
    }

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid? OrganizationId { get; private set; }
    public string? ProductCode { get; private set; }
    public Guid? PendingAccessRoleId { get; private set; }
    public string? PendingDepartment { get; private set; }
    public string? PendingJobTitle { get; private set; }
    public bool RequiresAccountActivation { get; private set; }
    public Guid? InvitedByUserId { get; private set; }
    public string TokenHash { get; private set; } = string.Empty;
    public string Status { get; private set; } = Statuses.Pending;
    public string PortalOrigin { get; private set; } = PortalOrigins.TenantPortal;
    public DateTime ExpiresAtUtc { get; private set; }
    public DateTime? AcceptedAtUtc { get; private set; }
    public DateTime? RevokedAtUtc { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    public User User { get; private set; } = null!;

    private UserInvitation() { }

    public static UserInvitation Create(
        Guid userId,
        Guid tenantId,
        string tokenHash,
        string portalOrigin = PortalOrigins.TenantPortal,
        Guid? invitedByUserId = null,
        int expiryHours = 72,
        Guid? organizationId = null,
        string? productCode = null,
        Guid? pendingAccessRoleId = null,
        string? pendingDepartment = null,
        string? pendingJobTitle = null,
        bool requiresAccountActivation = true)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tokenHash);

        var now = DateTime.UtcNow;
        return new UserInvitation
        {
            Id              = Guid.CreateVersion7(),
            UserId          = userId,
            TenantId        = tenantId,
            OrganizationId  = organizationId,
            ProductCode     = string.IsNullOrWhiteSpace(productCode) ? null : productCode.Trim().ToUpperInvariant(),
            PendingAccessRoleId = pendingAccessRoleId,
            PendingDepartment = NormalizeOptional(pendingDepartment),
            PendingJobTitle = NormalizeOptional(pendingJobTitle),
            RequiresAccountActivation = requiresAccountActivation,
            InvitedByUserId = invitedByUserId,
            TokenHash       = tokenHash,
            Status          = Statuses.Pending,
            PortalOrigin    = portalOrigin,
            ExpiresAtUtc    = now.AddHours(expiryHours),
            CreatedAtUtc    = now,
        };
    }

    public void Accept()
    {
        Status         = Statuses.Accepted;
        AcceptedAtUtc  = DateTime.UtcNow;
    }

    public void Revoke()
    {
        Status       = Statuses.Revoked;
        RevokedAtUtc = DateTime.UtcNow;
    }

    public bool IsExpired() => Status == Statuses.Pending && DateTime.UtcNow > ExpiresAtUtc;

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
