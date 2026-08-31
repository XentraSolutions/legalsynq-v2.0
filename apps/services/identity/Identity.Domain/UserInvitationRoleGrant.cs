namespace Identity.Domain;

/// <summary>
/// A product-role grant that is applied only when its parent invitation is accepted.
/// </summary>
public class UserInvitationRoleGrant
{
    public Guid Id { get; private set; }
    public Guid InvitationId { get; private set; }
    public Guid TenantId { get; private set; }
    public string ProductCode { get; private set; } = string.Empty;
    public string RoleCode { get; private set; } = string.Empty;
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? AppliedAtUtc { get; private set; }

    public UserInvitation Invitation { get; private set; } = null!;

    private UserInvitationRoleGrant() { }

    public static UserInvitationRoleGrant Create(
        Guid invitationId,
        Guid tenantId,
        string productCode,
        string roleCode)
    {
        if (invitationId == Guid.Empty) throw new ArgumentException("InvitationId is required.", nameof(invitationId));
        if (tenantId == Guid.Empty) throw new ArgumentException("TenantId is required.", nameof(tenantId));
        ArgumentException.ThrowIfNullOrWhiteSpace(productCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(roleCode);

        return new UserInvitationRoleGrant
        {
            Id = Guid.CreateVersion7(),
            InvitationId = invitationId,
            TenantId = tenantId,
            ProductCode = productCode.Trim().ToUpperInvariant(),
            RoleCode = roleCode.Trim().ToUpperInvariant(),
            CreatedAtUtc = DateTime.UtcNow,
        };
    }

    public void MarkApplied()
    {
        AppliedAtUtc ??= DateTime.UtcNow;
    }
}
