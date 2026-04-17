using Flow.Api.Services;

namespace Flow.Api.Middleware;

public class TenantMiddleware
{
    private readonly RequestDelegate _next;

    public TenantMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (!context.Request.Headers.ContainsKey(HttpTenantProvider.TenantHeaderName))
        {
            context.Request.Headers.Append(HttpTenantProvider.TenantHeaderName, HttpTenantProvider.DefaultTenantId);
        }

        await _next(context);
    }
}
