namespace Xenia.Api.Middleware;

/// <summary>
/// Propagates the X-Correlation-Id header through the request pipeline.
/// Generates a new correlation ID if none is provided.
/// The correlation ID is injected into the log scope for all log entries within the request.
/// </summary>
public sealed class XeniaCorrelationMiddleware
{
    private const string HeaderName = "X-Correlation-Id";
    private readonly RequestDelegate _next;

    public XeniaCorrelationMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = context.Request.Headers[HeaderName].FirstOrDefault()
            ?? Guid.CreateVersion7().ToString();

        context.Response.Headers[HeaderName] = correlationId;

        using var _ = context.RequestServices
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger("Xenia.Correlation")
            .BeginScope(new Dictionary<string, object>
            {
                ["CorrelationId"] = correlationId,
            });

        await _next(context);
    }
}
