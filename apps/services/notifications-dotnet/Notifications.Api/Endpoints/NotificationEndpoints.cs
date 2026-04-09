using Notifications.Api.Middleware;
using Notifications.Application.DTOs;
using Notifications.Application.Interfaces;

namespace Notifications.Api.Endpoints;

public static class NotificationEndpoints
{
    public static void MapNotificationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/v1/notifications").WithTags("Notifications");

        group.MapPost("/", async (HttpContext context, INotificationService service, SubmitNotificationDto request) =>
        {
            var tenantId = context.GetTenantId();
            var result = await service.SubmitAsync(tenantId, request);
            return result.Status == "blocked"
                ? Results.Json(result, statusCode: 422)
                : Results.Created($"/v1/notifications/{result.Id}", result);
        });

        group.MapGet("/{id:guid}", async (HttpContext context, INotificationService service, Guid id) =>
        {
            var tenantId = context.GetTenantId();
            var result = await service.GetByIdAsync(tenantId, id);
            return result != null ? Results.Ok(result) : Results.NotFound();
        });

        group.MapGet("/", async (HttpContext context, INotificationService service, int? limit, int? offset) =>
        {
            var tenantId = context.GetTenantId();
            var result = await service.ListAsync(tenantId, limit ?? 50, offset ?? 0);
            return Results.Ok(result);
        });
    }
}
