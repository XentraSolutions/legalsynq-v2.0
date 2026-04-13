namespace BuildingBlocks.Context;

public interface ICurrentRequestContext
{
    bool IsAuthenticated { get; }
    Guid? UserId { get; }
    Guid? TenantId { get; }
    string? TenantCode { get; }
    string? Email { get; }
    Guid? OrgId { get; }
    string? OrgType { get; }

    /// <summary>
    /// Phase B: canonical OrganizationType catalog ID from the org_type_id JWT claim.
    /// Null when the token was issued before org_type_id was added, or when the
    /// organization has not yet been assigned an OrganizationType.
    /// Prefer this over OrgType (string) in new code.
    /// </summary>
    Guid? OrgTypeId { get; }
    IReadOnlyCollection<string> Roles { get; }
    IReadOnlyCollection<string> ProductRoles { get; }
    IReadOnlyCollection<string> Permissions { get; }
    bool IsPlatformAdmin { get; }
}
