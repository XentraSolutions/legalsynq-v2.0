using Documents.Application.Exceptions;
using System.Text.Json;

namespace Documents.Api.Middleware;

public sealed class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _log;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> log)
    {
        _next = next;
        _log  = log;
    }

    public async Task InvokeAsync(HttpContext ctx)
    {
        try
        {
            await _next(ctx);
        }
        catch (Exception ex)
        {
            await HandleAsync(ctx, ex);
        }
    }

    private async Task HandleAsync(HttpContext ctx, Exception ex)
    {
        var correlationId = ctx.GetCorrelationId();

        object body;
        int    statusCode;

        switch (ex)
        {
            case DocumentsException de:
                statusCode = de.StatusCode;

                if (statusCode >= 500)
                    _log.LogError(ex, "Internal error [{Code}] correlationId={CorrelationId}", de.ErrorCode, correlationId);
                else
                    _log.LogWarning("Client error [{Code}] {Message} correlationId={CorrelationId}", de.ErrorCode, de.Message, correlationId);

                body = de is ValidationException ve
                    ? (object)new { error = de.ErrorCode, message = de.Message, details = ve.Details, correlationId }
                    : new { error = de.ErrorCode, message = de.Message, correlationId };
                break;

            case UnauthorizedAccessException ue:
                statusCode = 401;
                _log.LogWarning("Authentication failure: {Message}", ue.Message);
                body = new { error = "AUTHENTICATION_REQUIRED", message = "Bearer token required", correlationId };
                break;

            default:
                statusCode = 500;
                _log.LogError(ex, "Unhandled exception correlationId={CorrelationId}", correlationId);
                body = new { error = "INTERNAL_SERVER_ERROR", message = "An unexpected error occurred", correlationId };
                break;
        }

        ctx.Response.StatusCode  = statusCode;
        ctx.Response.ContentType = "application/json";
        await ctx.Response.WriteAsync(JsonSerializer.Serialize(body));
    }
}
