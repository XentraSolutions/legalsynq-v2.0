using System.Security.Claims;
using Identity.Api.Helpers;
using Identity.Application.DTOs;
using Identity.Application.Interfaces;
using Identity.Infrastructure.Data;
using Identity.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Identity.Api.Endpoints;

public static class SynqLienUserManagementEndpoints
{
    public static IEndpointRouteBuilder MapSynqLienUserManagementEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/internal/synqlien/user-management")
            .RequireAuthorization("SynqLienUserManagementInternal");

        group.MapGet("/users", ListUsersAsync)
            ;
        group.MapGet("/users/{userId:guid}", GetUserAsync)
            ;
        group.MapGet("/options", GetOptionsAsync);
        group.MapGet("/roles", ListRolesAsync);
        group.MapPost("/roles", CreateRoleAsync);
        group.MapPut("/roles/{roleId:guid}", UpdateRoleAsync);
        group.MapDelete("/roles/{roleId:guid}", DeleteRoleAsync);

        group.MapPost("/invitations", InviteAsync);
        group.MapPost("/users/{userId:guid}/invitations/resend", ResendAsync)
            ;
        group.MapDelete("/users/{userId:guid}/invitations/current", CancelAsync)
            ;

        group.MapPatch("/users/{userId:guid}/organization-profile", UpdateProfileAsync)
            ;
        group.MapPut("/users/{userId:guid}/role", ReplaceRoleAsync)
            ;
        group.MapPost("/users/{userId:guid}/activate", (
                HttpContext context, Guid userId, ISynqLienUserManagementService service, CancellationToken ct) =>
                SetProductAccessAsync(context, userId, true, service, ct))
            ;
        group.MapPost("/users/{userId:guid}/deactivate", (
                HttpContext context, Guid userId, ISynqLienUserManagementService service, CancellationToken ct) =>
                SetProductAccessAsync(context, userId, false, service, ct))
            ;

        return routes;
    }

    private static async Task<IResult> ListUsersAsync(
        HttpContext context,
        ISynqLienUserManagementService service,
        string? search = null,
        string? status = null,
        Guid? roleId = null,
        string? department = null,
        int page = 1,
        int pageSize = 25,
        string? sort = null,
        CancellationToken ct = default)
    {
        if (!TryGetScope(context, out var scope)) return Results.Unauthorized();
        return ToHttpResult(await service.ListAsync(scope,
            new SynqLienUserListQuery(search, status, roleId, department, page, pageSize, sort), ct));
    }

    private static async Task<IResult> GetUserAsync(
        HttpContext context, Guid userId, ISynqLienUserManagementService service, CancellationToken ct)
    {
        if (!TryGetScope(context, out var scope)) return Results.Unauthorized();
        return ToHttpResult(await service.GetAsync(scope, userId, ct));
    }

    private static async Task<IResult> GetOptionsAsync(
        HttpContext context, ISynqLienUserManagementService service, CancellationToken ct)
    {
        if (!TryGetScope(context, out var scope)) return Results.Unauthorized();
        return ToHttpResult(await service.GetOptionsAsync(scope, ct));
    }

    private static async Task<IResult> InviteAsync(
        HttpContext context,
        SynqLienInviteRequest request,
        ISynqLienUserManagementService service,
        IdentityDbContext db,
        INotificationsEmailClient emailClient,
        IOptions<NotificationsServiceOptions> notificationsOptions,
        ILoggerFactory loggerFactory,
        CancellationToken ct)
    {
        if (!TryGetScope(context, out var scope)) return Results.Unauthorized();
        var result = await service.InviteAsync(scope, request, ct);
        if (!result.IsSuccess) return ToHttpResult(result);

        var deliveryStatus = "NOT_REQUIRED";
        if (result.Value!.RawToken is not null)
        {
            deliveryStatus = await SubmitInvitationEmailAsync(
                result.Value, scope, db, emailClient, notificationsOptions.Value,
                loggerFactory.CreateLogger("Identity.Api.SynqLienUserManagement.Invite"), ct);
        }

        var response = new
        {
            result.Value.UserId,
            result.Value.InvitationId,
            result.Value.Email,
            result.Value.Outcome,
            DeliveryStatus = deliveryStatus,
        };
        return result.Value.Outcome == "INVITED"
            ? Results.Created($"/api/internal/synqlien/user-management/users/{result.Value.UserId}", response)
            : Results.Ok(response);
    }

    private static async Task<IResult> ResendAsync(
        HttpContext context,
        Guid userId,
        ISynqLienUserManagementService service,
        IdentityDbContext db,
        INotificationsEmailClient emailClient,
        IOptions<NotificationsServiceOptions> notificationsOptions,
        ILoggerFactory loggerFactory,
        CancellationToken ct)
    {
        if (!TryGetScope(context, out var scope)) return Results.Unauthorized();
        var result = await service.ResendInvitationAsync(scope, userId, ct);
        if (!result.IsSuccess) return ToHttpResult(result);

        var deliveryStatus = await SubmitInvitationEmailAsync(
            result.Value!, scope, db, emailClient, notificationsOptions.Value,
            loggerFactory.CreateLogger("Identity.Api.SynqLienUserManagement.Resend"), ct);
        return Results.Ok(new
        {
            result.Value!.UserId,
            result.Value.InvitationId,
            result.Value.Email,
            DeliveryStatus = deliveryStatus,
        });
    }

    private static async Task<IResult> CancelAsync(
        HttpContext context, Guid userId, ISynqLienUserManagementService service, CancellationToken ct)
    {
        if (!TryGetScope(context, out var scope)) return Results.Unauthorized();
        var result = await service.CancelInvitationAsync(scope, userId, ct);
        return result.IsSuccess ? Results.NoContent() : ToHttpResult(result);
    }

    private static async Task<IResult> UpdateProfileAsync(
        HttpContext context, Guid userId, SynqLienOrganizationProfileRequest request,
        ISynqLienUserManagementService service, CancellationToken ct)
    {
        if (!TryGetScope(context, out var scope)) return Results.Unauthorized();
        return ToHttpResult(await service.UpdateOrganizationProfileAsync(scope, userId, request, ct));
    }

    private static async Task<IResult> ReplaceRoleAsync(
        HttpContext context, Guid userId, SynqLienRoleRequest request,
        ISynqLienUserManagementService service, CancellationToken ct)
    {
        if (!TryGetScope(context, out var scope)) return Results.Unauthorized();
        return ToHttpResult(await service.ReplaceRoleAsync(scope, userId, request, ct));
    }

    private static async Task<IResult> SetProductAccessAsync(
        HttpContext context, Guid userId, bool activate,
        ISynqLienUserManagementService service, CancellationToken ct)
    {
        if (!TryGetScope(context, out var scope)) return Results.Unauthorized();
        var result = await service.SetProductAccessAsync(scope, userId, activate, ct);
        return result.IsSuccess ? Results.NoContent() : ToHttpResult(result);
    }

    private static async Task<IResult> ListRolesAsync(
        HttpContext context, ISynqLienUserManagementService service, CancellationToken ct)
    {
        if (!TryGetScope(context, out var scope)) return Results.Unauthorized();
        return ToHttpResult(await service.ListRolesAsync(scope, ct));
    }

    private static async Task<IResult> CreateRoleAsync(
        HttpContext context, SynqLienAccessRoleRequest request,
        ISynqLienUserManagementService service, CancellationToken ct)
    {
        if (!TryGetScope(context, out var scope)) return Results.Unauthorized();
        var result = await service.CreateRoleAsync(scope, request, ct);
        return result.IsSuccess
            ? Results.Created($"/api/internal/synqlien/user-management/roles/{result.Value!.Id}", result.Value)
            : ToHttpResult(result);
    }

    private static async Task<IResult> UpdateRoleAsync(
        HttpContext context, Guid roleId, SynqLienAccessRoleRequest request,
        ISynqLienUserManagementService service, CancellationToken ct)
    {
        if (!TryGetScope(context, out var scope)) return Results.Unauthorized();
        return ToHttpResult(await service.UpdateRoleAsync(scope, roleId, request, ct));
    }

    private static async Task<IResult> DeleteRoleAsync(
        HttpContext context, Guid roleId, ISynqLienUserManagementService service, CancellationToken ct)
    {
        if (!TryGetScope(context, out var scope)) return Results.Unauthorized();
        var result = await service.DeleteRoleAsync(scope, roleId, ct);
        return result.IsSuccess ? Results.NoContent() : ToHttpResult(result);
    }

    private static bool TryGetScope(HttpContext context, out SynqLienManagementScope scope)
    {
        var tenantValue = context.User.FindFirstValue("tenant_id");
        var organizationValue = context.Request.Headers["X-Organization-Id"].FirstOrDefault();
        var actorValue = context.User.FindFirstValue("actor")?.Replace("user:", "", StringComparison.OrdinalIgnoreCase);
        var correlationId = context.Request.Headers["X-Correlation-Id"].FirstOrDefault()
            ?? context.TraceIdentifier;

        if (Guid.TryParse(tenantValue, out var tenantId) && tenantId != Guid.Empty &&
            Guid.TryParse(organizationValue, out var organizationId) && organizationId != Guid.Empty &&
            Guid.TryParse(actorValue, out var actorId) && actorId != Guid.Empty)
        {
            scope = new SynqLienManagementScope(tenantId, organizationId, actorId, correlationId);
            return true;
        }

        scope = default!;
        return false;
    }

    private static async Task<string> SubmitInvitationEmailAsync(
        SynqLienInvitationMutation invitation,
        SynqLienManagementScope scope,
        IdentityDbContext db,
        INotificationsEmailClient emailClient,
        NotificationsServiceOptions options,
        ILogger logger,
        CancellationToken ct)
    {
        var tenant = await db.Tenants.Include(t => t.Domains)
            .SingleOrDefaultAsync(t => t.Id == scope.TenantId, ct);
        var link = invitation.RawToken is null
            ? null
            : TenantPortalUrlHelper.Build(tenant, "accept-invite", invitation.RawToken, options);
        if (link is null)
        {
            logger.LogWarning("SynqLien invitation {InvitationId} was committed but no portal URL is configured.", invitation.InvitationId);
            return "FAILED";
        }

        var (configured, success, error) = await emailClient.SendInviteEmailAsync(
            invitation.Email, invitation.DisplayName, link, scope.TenantId, ct);
        if (!configured || !success)
        {
            logger.LogWarning("SynqLien invitation {InvitationId} was committed but notification submission failed: {Error}.",
                invitation.InvitationId, error ?? "notifications-not-configured");
            return "FAILED";
        }

        return "SUBMITTED";
    }

    private static IResult ToHttpResult<T>(SynqLienUserManagementResult<T> result) => result.Error switch
    {
        SynqLienUserManagementError.None => Results.Ok(result.Value),
        SynqLienUserManagementError.Validation => Problem(StatusCodes.Status400BadRequest, result),
        SynqLienUserManagementError.Forbidden => Problem(StatusCodes.Status403Forbidden, result),
        SynqLienUserManagementError.Conflict => Problem(StatusCodes.Status409Conflict, result),
        _ => Results.NotFound(),
    };

    private static IResult Problem<T>(int status, SynqLienUserManagementResult<T> result) =>
        Results.Problem(statusCode: status, title: result.Message, extensions: new Dictionary<string, object?>
        {
            ["code"] = result.Code,
        });
}
