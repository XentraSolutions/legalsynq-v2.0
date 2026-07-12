using System.Text.Json;

namespace Xenia.Api.Middleware;

/// <summary>
/// Catches unhandled exceptions and returns a safe ProblemDetails-style response.
/// Never leaks stack traces, internal error details, or connection strings.
/// </summary>
public sealed class XeniaExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<XeniaExceptionMiddleware> _logger;

    public XeniaExceptionMiddleware(RequestDelegate next, ILogger<XeniaExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (KeyNotFoundException ex)
        {
            _logger.LogWarning(ex, "Xenia: resource not found — {Message}", ex.Message);
            await WriteErrorAsync(context, StatusCodes.Status404NotFound, "Resource not found.", ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Xenia: invalid operation — {Message}", ex.Message);
            await WriteErrorAsync(context, StatusCodes.Status409Conflict, "Conflict.", ex.Message);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Xenia: bad request — {Message}", ex.Message);
            await WriteErrorAsync(context, StatusCodes.Status400BadRequest, "Invalid request.", ex.Message);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Xenia: unauthorized — {Message}", ex.Message);
            await WriteErrorAsync(context, StatusCodes.Status403Forbidden, "Forbidden.", null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Xenia: unhandled exception processing {Method} {Path}",
                context.Request.Method, context.Request.Path);
            await WriteErrorAsync(context, StatusCodes.Status500InternalServerError,
                "An unexpected error occurred.", null);
        }
    }

    private static async Task WriteErrorAsync(
        HttpContext context, int statusCode, string title, string? detail)
    {
        if (context.Response.HasStarted) return;

        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/problem+json";

        var problem = new
        {
            type = $"https://httpstatuses.io/{statusCode}",
            title,
            status = statusCode,
            detail,
        };

        await context.Response.WriteAsync(
            JsonSerializer.Serialize(problem));
    }
}
