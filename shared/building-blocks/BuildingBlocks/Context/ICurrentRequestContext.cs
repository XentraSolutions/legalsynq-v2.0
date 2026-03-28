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
    IReadOnlyCollection<string> Roles { get; }
    IReadOnlyCollection<string> ProductRoles { get; }
    bool IsPlatformAdmin { get; }
}
