namespace PlatformAuditEventService.Middleware;

/// <summary>
/// Reads X-Correlation-ID from the incoming request and writes it back to the response.
/// If absent, generates a new correlation ID. Sets the value in HttpContext.Items
/// for downstream middleware and services.
/// </summary>
public sealed class CorrelationIdMiddleware
{
    private const string HeaderName = "X-Correlation-ID";
    private readonly RequestDelegate _next;

    public CorrelationIdMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext ctx)
    {
        var correlationId = ctx.Request.Headers[HeaderName].FirstOrDefault()
                         ?? Guid.NewGuid().ToString("D");

        ctx.Items["CorrelationId"]    = correlationId;
        ctx.Response.Headers[HeaderName] = correlationId;

        await _next(ctx);
    }
}
