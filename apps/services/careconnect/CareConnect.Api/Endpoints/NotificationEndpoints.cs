using BuildingBlocks.Authorization;
using BuildingBlocks.Authorization.Filters;
using BuildingBlocks.Context;
using CareConnect.Application.DTOs;
using CareConnect.Application.Interfaces;

namespace CareConnect.Api.Endpoints;

public static class NotificationEndpoints
{
    public static void MapNotificationEndpoints(this WebApplication app)
    {
        app.MapGet("/api/notifications", async (
            string?   status,
            string?   notificationType,
            string?   relatedEntityType,
            Guid?     relatedEntityId,
            DateTime? scheduledFrom,
            DateTime? scheduledTo,
            int?      page,
            int?      pageSize,
            INotificationService service,
            ICurrentRequestContext ctx,
            CancellationToken ct) =>
        {
            var tenantId = ctx.TenantId ?? throw new InvalidOperationException("tenant_id claim is missing.");

            var query = new GetNotificationsQuery
            {
                Status            = status,
                NotificationType  = notificationType,
                RelatedEntityType = relatedEntityType,
                RelatedEntityId   = relatedEntityId,
                ScheduledFrom     = scheduledFrom,
                ScheduledTo       = scheduledTo,
                Page              = page     ?? 1,
                PageSize          = pageSize ?? 20
            };

            var result = await service.SearchAsync(tenantId, query, ct);
            return Results.Ok(result);
        })
        .RequireAuthorization(Policies.AuthenticatedUser)
        .RequireProductAccess(ProductCodes.SynqCareConnect);

        app.MapGet("/api/notifications/{id:guid}", async (
            Guid id,
            INotificationService service,
            ICurrentRequestContext ctx,
            CancellationToken ct) =>
        {
            var tenantId = ctx.TenantId ?? throw new InvalidOperationException("tenant_id claim is missing.");
            var result = await service.GetByIdAsync(tenantId, id, ct);
            return Results.Ok(result);
        })
        .RequireAuthorization(Policies.AuthenticatedUser)
        .RequireProductAccess(ProductCodes.SynqCareConnect);
    }
}
