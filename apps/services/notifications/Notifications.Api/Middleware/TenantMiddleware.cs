namespace Notifications.Api.Middleware;

public class TenantMiddleware
{
    private readonly RequestDelegate _next;

    public TenantMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.Request.Path.StartsWithSegments("/health") ||
            context.Request.Path.StartsWithSegments("/info") ||
            context.Request.Path.StartsWithSegments("/v1/webhooks"))
        {
            await _next(context);
            return;
        }

        if (context.Request.Path.StartsWithSegments("/internal"))
        {
            await _next(context);
            return;
        }

        var tenantIdHeader = context.Request.Headers["X-Tenant-Id"].FirstOrDefault();
        if (string.IsNullOrEmpty(tenantIdHeader) || !Guid.TryParse(tenantIdHeader, out var tenantId))
        {
            context.Response.StatusCode = 400;
            await context.Response.WriteAsJsonAsync(new { error = "Missing or invalid X-Tenant-Id header" });
            return;
        }

        context.Items["TenantId"] = tenantId;
        await _next(context);
    }
}

public static class TenantMiddlewareExtensions
{
    public static Guid GetTenantId(this HttpContext context)
    {
        if (context.Items.TryGetValue("TenantId", out var tenantId) && tenantId is Guid id)
            return id;
        throw new InvalidOperationException("TenantId not found in request context");
    }
}
