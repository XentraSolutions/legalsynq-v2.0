using System.Net;
using System.Text.Json;
using PlatformAuditEventService.DTOs;
using PlatformAuditEventService.Utilities;

namespace PlatformAuditEventService.Middleware;

/// <summary>
/// Centralized exception handler. Catches unhandled exceptions, logs them,
/// and returns a structured JSON error response using the ApiResponse envelope.
/// Must be registered as the first middleware in the pipeline.
/// </summary>
public sealed class ExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionMiddleware> _logger;

    private static readonly JsonSerializerOptions _json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    public ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> logger)
    {
        _next   = next;
        _logger = logger;
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
        var traceId = TraceIdAccessor.Current() ?? ctx.TraceIdentifier;

        _logger.LogError(ex,
            "Unhandled exception on {Method} {Path}. TraceId={TraceId}",
            ctx.Request.Method, ctx.Request.Path, traceId);

        var (statusCode, message) = ex switch
        {
            ArgumentException       or
            InvalidOperationException => (HttpStatusCode.BadRequest,      ex.Message),
            UnauthorizedAccessException => (HttpStatusCode.Unauthorized,  "Unauthorized."),
            KeyNotFoundException      => (HttpStatusCode.NotFound,        "Resource not found."),
            _                         => (HttpStatusCode.InternalServerError, "An unexpected error occurred."),
        };

        var response = ApiResponse<object>.Fail(message, traceId: traceId);

        ctx.Response.ContentType = "application/json";
        ctx.Response.StatusCode  = (int)statusCode;

        await ctx.Response.WriteAsync(JsonSerializer.Serialize(response, _json));
    }
}
