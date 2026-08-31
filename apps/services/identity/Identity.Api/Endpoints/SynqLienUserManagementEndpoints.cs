using System.Security.Claims;
using BuildingBlocks.Authorization;
using BuildingBlocks.Authorization.Filters;
using Identity.Api.Helpers;
using Identity.Application.Interfaces;
using Identity.Infrastructure.Data;
using Identity.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Identity.Api.Endpoints;

public static class SynqLienUserManagementEndpoints
{
    public static void MapSynqLienUserManagementEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/products/SYNQ_LIENS/user-management")
            .RequireAuthorization(Policies.AuthenticatedUser)
            .AddEndpointFilter<SynqLienUserManagementExceptionFilter>();

        group.MapGet("/users", async (
            HttpContext http, ISynqLienUserManagementService service,
            string? search, string? status, string? roleCode, int page = 1, int pageSize = 25,
            CancellationToken ct = default) =>
        {
            var (tenantId, actorUserId) = RequireScope(http.User);
            return Results.Ok(await service.ListUsersAsync(
                tenantId, actorUserId, search, status, roleCode, page, pageSize, ct));
        }).RequirePermission(PermissionCodes.LienUserRead);

        group.MapGet("/users/{userId:guid}", async (
            HttpContext http, Guid userId, ISynqLienUserManagementService service,
            CancellationToken ct) =>
        {
            var (tenantId, actorUserId) = RequireScope(http.User);
            var result = await service.GetUserAsync(tenantId, actorUserId, userId, ct);
            SetEtag(http, result.AccessVersion);
            return Results.Ok(result);
        }).RequirePermission(PermissionCodes.LienUserRead);

        group.MapGet("/roles", async (
            HttpContext http, ISynqLienUserManagementService service, CancellationToken ct) =>
        {
            var (tenantId, actorUserId) = RequireScope(http.User);
            return Results.Ok(new { items = await service.ListRolesAsync(tenantId, actorUserId, ct) });
        }).RequirePermission(PermissionCodes.LienUserRoleAssign);

        group.MapGet("/invitations", async (
            HttpContext http, ISynqLienUserManagementService service,
            string? status, int page = 1, int pageSize = 25,
            CancellationToken ct = default) =>
        {
            var (tenantId, actorUserId) = RequireScope(http.User);
            return Results.Ok(await service.ListInvitationsAsync(
                tenantId, actorUserId, status, page, pageSize, ct));
        }).RequirePermission(PermissionCodes.LienUserInvite);

        group.MapPost("/invitations", async (
            HttpContext http,
            InviteSynqLienUserRequest body,
            ISynqLienUserManagementService service,
            IdentityDbContext db,
            INotificationsEmailClient emailClient,
            IOptions<NotificationsServiceOptions> notificationOptions,
            IWebHostEnvironment environment,
            ILoggerFactory loggerFactory,
            CancellationToken ct) =>
        {
            var (tenantId, actorUserId) = RequireScope(http.User);
            var result = await service.InviteAsync(
                tenantId, actorUserId,
                new SynqLienInviteCommand(body.Email, body.FirstName, body.LastName, body.Phone, body.RoleCodes ?? []),
                ct);
            var delivery = await DeliverInvitationAsync(
                result, tenantId, db, emailClient, notificationOptions.Value, environment,
                loggerFactory.CreateLogger("SynqLienUserManagement.Invite"), ct);
            return Results.Created(
                $"/api/v1/products/SYNQ_LIENS/user-management/users/{result.UserId}", delivery);
        }).RequirePermission(PermissionCodes.LienUserInvite);

        group.MapPost("/invitations/{invitationId:guid}/resend", async (
            HttpContext http,
            Guid invitationId,
            ISynqLienUserManagementService service,
            IdentityDbContext db,
            INotificationsEmailClient emailClient,
            IOptions<NotificationsServiceOptions> notificationOptions,
            IWebHostEnvironment environment,
            ILoggerFactory loggerFactory,
            CancellationToken ct) =>
        {
            var (tenantId, actorUserId) = RequireScope(http.User);
            var result = await service.ResendInvitationAsync(tenantId, actorUserId, invitationId, ct);
            var delivery = await DeliverInvitationAsync(
                result, tenantId, db, emailClient, notificationOptions.Value, environment,
                loggerFactory.CreateLogger("SynqLienUserManagement.Resend"), ct);
            return Results.Ok(delivery);
        }).RequirePermission(PermissionCodes.LienUserInvite);

        group.MapDelete("/invitations/{invitationId:guid}", async (
            HttpContext http, Guid invitationId, ISynqLienUserManagementService service,
            CancellationToken ct) =>
        {
            var (tenantId, actorUserId) = RequireScope(http.User);
            await service.CancelInvitationAsync(tenantId, actorUserId, invitationId, ct);
            return Results.NoContent();
        }).RequirePermission(PermissionCodes.LienUserInvite);

        group.MapPut("/users/{userId:guid}/access", async (
            HttpContext http, Guid userId, SetSynqLienUserAccessRequest body,
            ISynqLienUserManagementService service, CancellationToken ct) =>
        {
            var expectedVersion = RequireIfMatch(http);
            var (tenantId, actorUserId) = RequireScope(http.User);
            var result = await service.SetAccessAsync(
                tenantId, actorUserId, userId, body.Enabled, expectedVersion, ct);
            SetEtag(http, result.AccessVersion);
            return Results.Ok(result);
        }).RequirePermission(PermissionCodes.LienUserAccessManage);

        group.MapPut("/users/{userId:guid}/roles", async (
            HttpContext http, Guid userId, ReplaceSynqLienUserRolesRequest body,
            ISynqLienUserManagementService service, CancellationToken ct) =>
        {
            var expectedVersion = RequireIfMatch(http);
            var (tenantId, actorUserId) = RequireScope(http.User);
            var result = await service.ReplaceRolesAsync(
                tenantId, actorUserId, userId, body.RoleCodes ?? [], expectedVersion, ct);
            SetEtag(http, result.AccessVersion);
            return Results.Ok(result);
        }).RequirePermission(PermissionCodes.LienUserRoleAssign);
    }

    private static (Guid TenantId, Guid ActorUserId) RequireScope(ClaimsPrincipal user)
    {
        var tenantRaw = user.FindFirstValue("tenant_id");
        var userRaw = user.FindFirstValue("sub") ?? user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(tenantRaw, out var tenantId) || !Guid.TryParse(userRaw, out var actorUserId))
            throw new SynqLienUserManagementException(
                401, "AUTHENTICATION_REQUIRED", "The token is missing a valid tenant_id or user identifier.");
        return (tenantId, actorUserId);
    }

    private static int RequireIfMatch(HttpContext http)
    {
        if (!http.Request.Headers.TryGetValue("If-Match", out var values))
            throw new SynqLienUserManagementException(
                StatusCodes.Status428PreconditionRequired,
                "IF_MATCH_REQUIRED",
                "If-Match with the current access version is required.");

        var raw = values.ToString().Trim();
        if (raw.StartsWith("W/", StringComparison.OrdinalIgnoreCase)) raw = raw[2..].Trim();
        raw = raw.Trim('"');
        if (!int.TryParse(raw, out var version) || version < 0)
            throw new SynqLienUserManagementException(
                400, "INVALID_IF_MATCH", "If-Match must contain a valid non-negative access version.");
        return version;
    }

    private static void SetEtag(HttpContext http, int accessVersion) =>
        http.Response.Headers.ETag = $"\"{accessVersion}\"";

    private static async Task<object> DeliverInvitationAsync(
        SynqLienInviteResult result,
        Guid tenantId,
        IdentityDbContext db,
        INotificationsEmailClient emailClient,
        NotificationsServiceOptions options,
        IWebHostEnvironment environment,
        ILogger logger,
        CancellationToken ct)
    {
        var tenant = await db.Tenants.Include(t => t.Domains).FirstAsync(t => t.Id == tenantId, ct);
        var user = await db.Users.AsNoTracking().FirstAsync(u => u.Id == result.UserId, ct);
        var displayName = $"{user.FirstName} {user.LastName}".Trim();

        if (result.AccessGrantedImmediately)
        {
            var portalUrl = TenantPortalUrlHelper.BuildBaseUrl(tenant, options);
            var delivery = portalUrl is null
                ? (EmailConfigured: false, Success: false, Error: "Tenant portal URL is not configured.")
                : await emailClient.SendTenantAccessGrantedEmailAsync(
                    user.Email, displayName, tenant.Name, portalUrl, tenantId, ct);
            LogDeliveryFailure(logger, result.UserId, delivery);
            return new
            {
                result.UserId,
                result.InvitationId,
                result.Email,
                result.IsNewUser,
                result.AccessGrantedImmediately,
                emailDelivery = DeliveryStatus(delivery),
            };
        }

        var activationLink = result.RawToken is null
            ? null
            : TenantPortalUrlHelper.Build(tenant, "accept-invite", result.RawToken, options);
        var inviteDelivery = activationLink is null
            ? (EmailConfigured: false, Success: false, Error: "Tenant portal URL is not configured.")
            : await emailClient.SendInviteEmailAsync(
                user.Email, displayName, activationLink, tenantId, ct);
        LogDeliveryFailure(logger, result.UserId, inviteDelivery);

        return new
        {
            result.UserId,
            result.InvitationId,
            result.Email,
            result.IsNewUser,
            result.AccessGrantedImmediately,
            emailDelivery = DeliveryStatus(inviteDelivery),
            inviteToken = environment.IsProduction() ? null : result.RawToken,
            activationLink = environment.IsProduction() ? null : activationLink,
        };
    }

    private static object DeliveryStatus((bool EmailConfigured, bool Success, string? Error) delivery) =>
        new
        {
            configured = delivery.EmailConfigured,
            accepted = delivery.Success,
            error = delivery.Success ? null : delivery.Error,
        };

    private static void LogDeliveryFailure(
        ILogger logger, Guid userId, (bool EmailConfigured, bool Success, string? Error) delivery)
    {
        if (!delivery.Success)
            logger.LogWarning(
                "SynqLien user-management email was not accepted for user {UserId}: configured={Configured} error={Error}",
                userId, delivery.EmailConfigured, delivery.Error);
    }
}

public sealed record InviteSynqLienUserRequest(
    string Email,
    string FirstName,
    string LastName,
    string? Phone,
    IReadOnlyCollection<string>? RoleCodes);

public sealed record SetSynqLienUserAccessRequest(bool Enabled);
public sealed record ReplaceSynqLienUserRolesRequest(IReadOnlyCollection<string>? RoleCodes);

public sealed class SynqLienUserManagementExceptionFilter : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        try
        {
            return await next(context);
        }
        catch (SynqLienUserManagementException ex)
        {
            return Results.Problem(
                statusCode: ex.StatusCode,
                title: ex.Code,
                detail: ex.Message,
                extensions: new Dictionary<string, object?> { ["code"] = ex.Code });
        }
    }
}
