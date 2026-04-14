using System.Diagnostics;

namespace Reports.Api.Middleware;

public sealed class RequestLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestLoggingMiddleware> _log;

    public RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> log)
    {
        _next = next;
        _log  = log;
    }

    public async Task InvokeAsync(HttpContext ctx)
    {
        var correlationId = ctx.Request.Headers["X-Correlation-Id"].FirstOrDefault()
                         ?? ctx.TraceIdentifier;

        ctx.Response.Headers["X-Correlation-Id"] = correlationId;

        var sw = Stopwatch.StartNew();

        using (_log.BeginScope(new Dictionary<string, object>
        {
            ["CorrelationId"] = correlationId,
            ["RequestPath"]   = ctx.Request.Path.ToString(),
            ["Method"]        = ctx.Request.Method,
        }))
        {
            _log.LogInformation("→ {Method} {Path}", ctx.Request.Method, ctx.Request.Path);

            await _next(ctx);

            sw.Stop();
            _log.LogInformation("← {Method} {Path} responded {StatusCode} in {ElapsedMs}ms",
                ctx.Request.Method, ctx.Request.Path, ctx.Response.StatusCode, sw.ElapsedMilliseconds);
        }
    }
}
