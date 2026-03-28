namespace BuildingBlocks.Context;

public interface ICurrentRequestContext
{
    bool IsAuthenticated { get; }
    Guid? UserId { get; }
    Guid? TenantId { get; }
    string? TenantCode { get; }
    string? Email { get; }
    IReadOnlyCollection<string> Roles { get; }
}
