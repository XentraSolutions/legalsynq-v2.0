using Notifications.Api.Middleware;
using Notifications.Application.DTOs;
using Notifications.Application.Interfaces;

namespace Notifications.Api.Endpoints;

public static class NotificationEndpoints
{
    public static void MapNotificationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/v1/notifications").WithTags("Notifications");

        // ── POST /v1/notifications ────────────────────────────────────────────
        group.MapPost("/", async (HttpContext context, INotificationService service, SubmitNotificationDto request) =>
        {
            var tenantId = context.GetTenantId();
            var result = await service.SubmitAsync(tenantId, request);
            return result.Status == "blocked"
                ? Results.Json(result, statusCode: 422)
                : Results.Created($"/v1/notifications/{result.Id}", result);
        });

        // ── GET /v1/notifications/stats ───────────────────────────────────────
        // Must be registered BEFORE /{id:guid} to avoid routing ambiguity
        group.MapGet("/stats", async (
            HttpContext context,
            INotificationService service,
            DateTime? from,
            DateTime? to,
            string? channel,
            string? status,
            string? provider,
            string? productKey) =>
        {
            var tenantId = context.GetTenantId();
            var query = new NotificationStatsQuery
            {
                From       = from,
                To         = to,
                Channel    = channel,
                Status     = status,
                Provider   = provider,
                ProductKey = productKey,
            };
            var result = await service.GetStatsAsync(tenantId, query);
            return Results.Ok(result);
        });

        // ── GET /v1/notifications ─────────────────────────────────────────────
        group.MapGet("/", async (
            HttpContext context,
            INotificationService service,
            int? page,
            int? pageSize,
            string? status,
            string? channel,
            string? provider,
            string? recipient,
            string? productKey,
            DateTime? from,
            DateTime? to,
            string? sortBy,
            string? sortDirection,
            // Backward-compat params still supported
            int? limit,
            int? offset) =>
        {
            var tenantId = context.GetTenantId();

            // If any paged/filter params provided, use paged response
            var usesPaged = page.HasValue || pageSize.HasValue || status != null || channel != null
                         || provider != null || recipient != null || productKey != null
                         || from.HasValue || to.HasValue || sortBy != null || sortDirection != null;

            if (usesPaged)
            {
                var query = new NotificationListQuery
                {
                    Page          = page ?? 1,
                    PageSize      = pageSize ?? limit ?? 50,
                    Status        = status,
                    Channel       = channel,
                    Provider      = provider,
                    Recipient     = recipient,
                    ProductKey    = productKey,
                    From          = from,
                    To            = to,
                    SortBy        = sortBy,
                    SortDirection = sortDirection,
                };
                var result = await service.ListPagedAsync(tenantId, query);
                return Results.Ok(result);
            }

            // Legacy path: backward-compatible raw list
            var items = await service.ListAsync(tenantId, limit ?? 50, offset ?? 0);
            return Results.Ok(items);
        });

        // ── GET /v1/notifications/{id} ────────────────────────────────────────
        group.MapGet("/{id:guid}", async (HttpContext context, INotificationService service, Guid id) =>
        {
            var tenantId = context.GetTenantId();
            var result = await service.GetByIdAsync(tenantId, id);
            return result != null ? Results.Ok(result) : Results.NotFound();
        });

        // ── GET /v1/notifications/{id}/events ─────────────────────────────────
        group.MapGet("/{id:guid}/events", async (HttpContext context, INotificationService service, Guid id) =>
        {
            var tenantId = context.GetTenantId();
            var result = await service.GetEventsAsync(tenantId, id);
            if (result.Count == 0)
            {
                // Distinguish "notification not found" from "no events yet"
                var exists = await service.GetByIdAsync(tenantId, id);
                if (exists == null) return Results.NotFound();
            }
            return Results.Ok(result);
        });

        // ── GET /v1/notifications/{id}/issues ─────────────────────────────────
        group.MapGet("/{id:guid}/issues", async (HttpContext context, INotificationService service, Guid id) =>
        {
            var tenantId = context.GetTenantId();
            var result = await service.GetIssuesAsync(tenantId, id);
            // GetIssuesAsync returns empty list if notification not found
            var exists = await service.GetByIdAsync(tenantId, id);
            if (exists == null) return Results.NotFound();
            return Results.Ok(result);
        });

        // ── POST /v1/notifications/{id}/retry ────────────────────────────────
        group.MapPost("/{id:guid}/retry", async (HttpContext context, INotificationService service, Guid id) =>
        {
            var tenantId = context.GetTenantId();
            var result = await service.RetryAsync(tenantId, id);
            if (result == null) return Results.NotFound();

            if (result.FailureCategory == "not_retryable")
                return Results.Json(result, statusCode: 422);

            return Results.Ok(result);
        });

        // ── POST /v1/notifications/{id}/resend ───────────────────────────────
        group.MapPost("/{id:guid}/resend", async (HttpContext context, INotificationService service, Guid id) =>
        {
            var tenantId = context.GetTenantId();
            var result = await service.ResendAsync(tenantId, id);
            if (result == null) return Results.NotFound();
            return Results.Created($"/v1/notifications/{result.NewNotificationId}", result);
        });
    }
}
