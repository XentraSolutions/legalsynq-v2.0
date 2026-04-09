using Identity.Application.DTOs;
using Identity.Domain;

namespace Identity.Application.Interfaces;

public interface IProductRoleMapper
{
    string ProductCode { get; }

    IReadOnlyList<ProductAccessEntry> ResolveRoles(
        ProductRoleMapperContext context);
}

public sealed record ProductRoleMapperContext
{
    public Guid UserId { get; init; }
    public Guid TenantId { get; init; }
    public Organization Organization { get; init; } = null!;
    public UserOrganizationMembership Membership { get; init; } = null!;
    public IReadOnlyList<string> SystemRoles { get; init; } = [];
    public IReadOnlyList<ScopedRoleAssignment> ScopedRoleAssignments { get; init; } = [];
    public IReadOnlyList<ProductRole> AvailableProductRoles { get; init; } = [];
}
