using System.Security.Claims;
using Identity.Application.DTOs;
using Identity.Application.Interfaces;
using LegalSynq.AuditClient;
using LegalSynq.AuditClient.DTOs;
using LegalSynq.AuditClient.Enums;

namespace Identity.Api.Endpoints;

public static class UserEndpoints
{
    public static void MapUserEndpoints(this WebApplication app)
    {
        app.MapPost("/api/users", async (
            CreateUserRequest request,
            IUserService      userService,
            IAuditEventClient auditClient,
            CancellationToken ct) =>
        {
            try
            {
                var user = await userService.CreateUserAsync(request, ct);

                // Canonical audit: identity.user.created — fire-and-observe.
                var now = DateTimeOffset.UtcNow;
                _ = auditClient.IngestAsync(new IngestAuditEventRequest
                {
                    EventType     = "identity.user.created",
                    EventCategory = EventCategory.Administrative,
                    SourceSystem  = "identity-service",
                    SourceService = "user-api",
                    Visibility    = VisibilityScope.Tenant,
                    Severity      = SeverityLevel.Info,
                    OccurredAtUtc = now,
                    Scope = new AuditEventScopeDto
                    {
                        ScopeType = ScopeType.Tenant,
                        TenantId  = user.TenantId.ToString(),
                    },
                    Actor = new AuditEventActorDto
                    {
                        Type = ActorType.System,
                        Name = "identity-service",
                    },
                    Entity = new AuditEventEntityDto { Type = "User", Id = user.Id.ToString() },
                    Action      = "UserCreated",
                    Description = $"User '{user.Email}' created in tenant {user.TenantId}.",
                    After       = System.Text.Json.JsonSerializer.Serialize(new
                    {
                        userId = user.Id,
                        email  = user.Email,
                        tenantId = user.TenantId,
                    }),
                    IdempotencyKey = IdempotencyKey.For("identity-service", "identity.user.created", user.Id.ToString()),
                    Tags = ["user-management", "provisioning"],
                });

                return Results.Created($"/api/users/{user.Id}", user);
            }
            catch (InvalidOperationException ex)
            {
                return Results.Problem(ex.Message, statusCode: 400);
            }
        });

        app.MapGet("/api/users", async (
            ClaimsPrincipal   caller,
            IUserService      userService,
            CancellationToken ct) =>
        {
            var tenantIdStr = caller.FindFirstValue("tenant_id");
            if (!Guid.TryParse(tenantIdStr, out var tenantId))
                return Results.Unauthorized();

            var users = await userService.GetByTenantAsync(tenantId, ct);
            return Results.Ok(users);
        }).RequireAuthorization();

        app.MapGet("/api/users/{id:guid}", async (Guid id, IUserService userService, CancellationToken ct) =>
        {
            var user = await userService.GetByIdAsync(id, ct);
            return user is null ? Results.NotFound() : Results.Ok(user);
        });
    }
}
