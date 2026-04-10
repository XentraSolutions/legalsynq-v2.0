using System.Security.Claims;
using Identity.Application.Interfaces;
using Identity.Domain;

namespace Identity.Api.Endpoints;

public static class GroupEndpoints
{
    public static IEndpointRouteBuilder MapGroupEndpoints(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/api/tenants/{tenantId:guid}/groups", ListGroups);
        routes.MapPost("/api/tenants/{tenantId:guid}/groups", CreateGroup);
        routes.MapGet("/api/tenants/{tenantId:guid}/groups/{groupId:guid}", GetGroup);
        routes.MapPatch("/api/tenants/{tenantId:guid}/groups/{groupId:guid}", UpdateGroup);
        routes.MapDelete("/api/tenants/{tenantId:guid}/groups/{groupId:guid}", ArchiveGroup);

        routes.MapGet("/api/tenants/{tenantId:guid}/groups/{groupId:guid}/members", ListMembers);
        routes.MapPost("/api/tenants/{tenantId:guid}/groups/{groupId:guid}/members", AddMember);
        routes.MapDelete("/api/tenants/{tenantId:guid}/groups/{groupId:guid}/members/{userId:guid}", RemoveMember);
        routes.MapGet("/api/tenants/{tenantId:guid}/users/{userId:guid}/groups", ListUserGroups);

        routes.MapGet("/api/tenants/{tenantId:guid}/groups/{groupId:guid}/products", ListGroupProducts);
        routes.MapPut("/api/tenants/{tenantId:guid}/groups/{groupId:guid}/products/{productCode}", GrantGroupProduct);
        routes.MapDelete("/api/tenants/{tenantId:guid}/groups/{groupId:guid}/products/{productCode}", RevokeGroupProduct);

        routes.MapGet("/api/tenants/{tenantId:guid}/groups/{groupId:guid}/roles", ListGroupRoles);
        routes.MapPost("/api/tenants/{tenantId:guid}/groups/{groupId:guid}/roles", AssignGroupRole);
        routes.MapDelete("/api/tenants/{tenantId:guid}/groups/{groupId:guid}/roles/{assignmentId:guid}", RemoveGroupRole);

        return routes;
    }

    private static Guid? GetActorUserId(HttpContext ctx)
    {
        var sub = ctx.User.FindFirstValue("sub") ?? ctx.User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(sub, out var id) ? id : null;
    }

    private static Guid? GetActorTenantId(HttpContext ctx)
    {
        var tid = ctx.User.FindFirstValue("tenantId") ?? ctx.User.FindFirstValue("tenant_id");
        return Guid.TryParse(tid, out var id) ? id : null;
    }

    private static bool IsPlatformAdmin(HttpContext ctx) =>
        ctx.User.IsInRole("PlatformAdmin") || ctx.User.IsInRole("SuperAdmin");

    private static bool CanReadTenant(HttpContext ctx, Guid tenantId)
    {
        if (IsPlatformAdmin(ctx)) return true;
        return GetActorTenantId(ctx) == tenantId;
    }

    private static bool CanMutateTenant(HttpContext ctx, Guid tenantId)
    {
        if (IsPlatformAdmin(ctx)) return true;
        if (!ctx.User.IsInRole("TenantAdmin")) return false;
        return GetActorTenantId(ctx) == tenantId;
    }

    private static async Task<IResult> ListGroups(Guid tenantId, IGroupService svc, HttpContext ctx)
    {
        if (!CanReadTenant(ctx, tenantId)) return Results.Forbid();
        var items = await svc.ListByTenantAsync(tenantId);
        return Results.Ok(items.Select(g => MapGroup(g)));
    }

    private static async Task<IResult> CreateGroup(Guid tenantId, CreateGroupRequest body, IGroupService svc, HttpContext ctx)
    {
        if (!CanMutateTenant(ctx, tenantId)) return Results.Forbid();
        if (string.IsNullOrWhiteSpace(body.Name)) return Results.BadRequest(new { error = "Name is required." });

        try
        {
            var result = await svc.CreateAsync(tenantId, body.Name, body.Description,
                body.ScopeType, body.ProductCode, body.OrganizationId, GetActorUserId(ctx));
            return Results.Created($"/api/tenants/{tenantId}/groups/{result.Id}", MapGroup(result));
        }
        catch (InvalidOperationException ex) { return Results.BadRequest(new { error = ex.Message }); }
    }

    private static async Task<IResult> GetGroup(Guid tenantId, Guid groupId, IGroupService svc, HttpContext ctx)
    {
        if (!CanReadTenant(ctx, tenantId)) return Results.Forbid();
        var group = await svc.GetByIdAsync(tenantId, groupId);
        return group == null ? Results.NotFound() : Results.Ok(MapGroup(group));
    }

    private static async Task<IResult> UpdateGroup(Guid tenantId, Guid groupId, UpdateGroupRequest body, IGroupService svc, HttpContext ctx)
    {
        if (!CanMutateTenant(ctx, tenantId)) return Results.Forbid();
        if (string.IsNullOrWhiteSpace(body.Name)) return Results.BadRequest(new { error = "Name is required." });

        try
        {
            var result = await svc.UpdateAsync(tenantId, groupId, body.Name, body.Description, GetActorUserId(ctx));
            return Results.Ok(MapGroup(result));
        }
        catch (InvalidOperationException ex) { return Results.BadRequest(new { error = ex.Message }); }
    }

    private static async Task<IResult> ArchiveGroup(Guid tenantId, Guid groupId, IGroupService svc, HttpContext ctx)
    {
        if (!CanMutateTenant(ctx, tenantId)) return Results.Forbid();
        var archived = await svc.ArchiveAsync(tenantId, groupId, GetActorUserId(ctx));
        return archived ? Results.NoContent() : Results.NotFound();
    }

    private static async Task<IResult> ListMembers(Guid tenantId, Guid groupId, IGroupMembershipService svc, HttpContext ctx)
    {
        if (!CanReadTenant(ctx, tenantId)) return Results.Forbid();
        var items = await svc.ListMembersAsync(tenantId, groupId);
        return Results.Ok(items.Select(m => new
        {
            m.Id, m.TenantId, m.GroupId, m.UserId,
            MembershipStatus = m.MembershipStatus.ToString(),
            m.AddedAtUtc, m.RemovedAtUtc
        }));
    }

    private static async Task<IResult> AddMember(Guid tenantId, Guid groupId, AddMemberRequest body, IGroupMembershipService svc, HttpContext ctx)
    {
        if (!CanMutateTenant(ctx, tenantId)) return Results.Forbid();
        if (body.UserId == Guid.Empty) return Results.BadRequest(new { error = "UserId is required." });

        try
        {
            var result = await svc.AddMemberAsync(tenantId, groupId, body.UserId, GetActorUserId(ctx));
            return Results.Created($"/api/tenants/{tenantId}/groups/{groupId}/members", new
            {
                result.Id, result.TenantId, result.GroupId, result.UserId,
                MembershipStatus = result.MembershipStatus.ToString(), result.AddedAtUtc
            });
        }
        catch (InvalidOperationException ex) { return Results.BadRequest(new { error = ex.Message }); }
    }

    private static async Task<IResult> RemoveMember(Guid tenantId, Guid groupId, Guid userId, IGroupMembershipService svc, HttpContext ctx)
    {
        if (!CanMutateTenant(ctx, tenantId)) return Results.Forbid();
        var removed = await svc.RemoveMemberAsync(tenantId, groupId, userId, GetActorUserId(ctx));
        return removed ? Results.NoContent() : Results.NotFound();
    }

    private static async Task<IResult> ListUserGroups(Guid tenantId, Guid userId, IGroupMembershipService svc, HttpContext ctx)
    {
        if (!CanReadTenant(ctx, tenantId)) return Results.Forbid();
        var items = await svc.ListGroupsForUserAsync(tenantId, userId);
        return Results.Ok(items.Select(m => new
        {
            m.Id, m.TenantId, m.GroupId, m.UserId,
            MembershipStatus = m.MembershipStatus.ToString(),
            m.AddedAtUtc, m.RemovedAtUtc
        }));
    }

    private static async Task<IResult> ListGroupProducts(Guid tenantId, Guid groupId, IGroupProductAccessService svc, HttpContext ctx)
    {
        if (!CanReadTenant(ctx, tenantId)) return Results.Forbid();
        var items = await svc.ListAsync(tenantId, groupId);
        return Results.Ok(items.Select(a => new
        {
            a.Id, a.TenantId, a.GroupId, a.ProductCode,
            AccessStatus = a.AccessStatus.ToString(),
            a.GrantedAtUtc, a.RevokedAtUtc
        }));
    }

    private static async Task<IResult> GrantGroupProduct(Guid tenantId, Guid groupId, string productCode, IGroupProductAccessService svc, HttpContext ctx)
    {
        if (!CanMutateTenant(ctx, tenantId)) return Results.Forbid();
        try
        {
            var result = await svc.GrantAsync(tenantId, groupId, productCode, GetActorUserId(ctx));
            return Results.Ok(new
            {
                result.Id, result.TenantId, result.GroupId, result.ProductCode,
                AccessStatus = result.AccessStatus.ToString(), result.GrantedAtUtc
            });
        }
        catch (InvalidOperationException ex) { return Results.BadRequest(new { error = ex.Message }); }
    }

    private static async Task<IResult> RevokeGroupProduct(Guid tenantId, Guid groupId, string productCode, IGroupProductAccessService svc, HttpContext ctx)
    {
        if (!CanMutateTenant(ctx, tenantId)) return Results.Forbid();
        var revoked = await svc.RevokeAsync(tenantId, groupId, productCode, GetActorUserId(ctx));
        return revoked ? Results.NoContent() : Results.NotFound();
    }

    private static async Task<IResult> ListGroupRoles(Guid tenantId, Guid groupId, IGroupRoleAssignmentService svc, HttpContext ctx)
    {
        if (!CanReadTenant(ctx, tenantId)) return Results.Forbid();
        var items = await svc.ListAsync(tenantId, groupId);
        return Results.Ok(items.Select(a => new
        {
            a.Id, a.TenantId, a.GroupId, a.RoleCode, a.ProductCode, a.OrganizationId,
            AssignmentStatus = a.AssignmentStatus.ToString(),
            a.AssignedAtUtc, a.RemovedAtUtc
        }));
    }

    private static async Task<IResult> AssignGroupRole(Guid tenantId, Guid groupId, AssignGroupRoleRequest body, IGroupRoleAssignmentService svc, HttpContext ctx)
    {
        if (!CanMutateTenant(ctx, tenantId)) return Results.Forbid();
        if (string.IsNullOrWhiteSpace(body.RoleCode)) return Results.BadRequest(new { error = "RoleCode is required." });

        try
        {
            var result = await svc.AssignAsync(tenantId, groupId, body.RoleCode, body.ProductCode, body.OrganizationId, GetActorUserId(ctx));
            return Results.Created($"/api/tenants/{tenantId}/groups/{groupId}/roles/{result.Id}", new
            {
                result.Id, result.TenantId, result.GroupId, result.RoleCode, result.ProductCode, result.OrganizationId,
                AssignmentStatus = result.AssignmentStatus.ToString(), result.AssignedAtUtc
            });
        }
        catch (InvalidOperationException ex) { return Results.BadRequest(new { error = ex.Message }); }
    }

    private static async Task<IResult> RemoveGroupRole(Guid tenantId, Guid groupId, Guid assignmentId, IGroupRoleAssignmentService svc, HttpContext ctx)
    {
        if (!CanMutateTenant(ctx, tenantId)) return Results.Forbid();
        var removed = await svc.RemoveAsync(tenantId, groupId, assignmentId, GetActorUserId(ctx));
        return removed ? Results.NoContent() : Results.NotFound();
    }

    private static object MapGroup(AccessGroup g) => new
    {
        g.Id, g.TenantId, g.Name, g.Description,
        Status = g.Status.ToString(),
        ScopeType = g.ScopeType.ToString(),
        g.ProductCode, g.OrganizationId,
        g.CreatedAtUtc, g.UpdatedAtUtc
    };
}

public record CreateGroupRequest(string Name, string? Description = null, GroupScopeType ScopeType = GroupScopeType.Tenant, string? ProductCode = null, Guid? OrganizationId = null);
public record UpdateGroupRequest(string Name, string? Description = null);
public record AddMemberRequest(Guid UserId);
public record AssignGroupRoleRequest(string RoleCode, string? ProductCode = null, Guid? OrganizationId = null);
