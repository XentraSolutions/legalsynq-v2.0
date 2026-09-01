using BuildingBlocks.Exceptions;
using Liens.Api;

namespace Liens.Api.Middleware;

public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
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
        catch (ValidationException ex)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(new
            {
                error = new
                {
                    code = "validation_error",
                    message = ex.Message,
                    details = ex.Errors
                }
            });
        }
        catch (NotFoundException ex)
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(new
            {
                error = new
                {
                    code = "not_found",
                    message = ex.Message
                }
            });
        }
        catch (ConflictException ex)
        {
            context.Response.StatusCode = StatusCodes.Status409Conflict;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(new
            {
                error = new
                {
                    code = "conflict",
                    message = ex.Message
                }
            });
        }
        catch (InvalidOperationException ex)
        {
            context.Response.StatusCode = StatusCodes.Status409Conflict;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(new
            {
                error = new
                {
                    code = "business_rule_violation",
                    message = ex.Message
                }
            });
        }
        catch (ServiceUnavailableException ex)
        {
            context.Response.StatusCode = StatusCodes.Status502BadGateway;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(new
            {
                error = new
                {
                    code = "service_unavailable",
                    message = ex.Message
                }
            });
        }
        catch (UnauthorizedAccessException ex)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(new
            {
                error = new
                {
                    code = "unauthorized",
                    message = ex.Message
                }
            });
        }
        catch (BadHttpRequestException ex) when (ex.StatusCode == StatusCodes.Status413PayloadTooLarge)
        {
            context.Response.StatusCode = StatusCodes.Status413PayloadTooLarge;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(new
            {
                error = new
                {
                    code = "file_too_large",
                    message = $"The uploaded file exceeds the maximum allowed size of {LiensUploadLimits.MaxMegabytes} MB."
                }
            });
        }
        catch (InvalidDataException ex) when (IsMultipartBodyLimitError(ex))
        {
            context.Response.StatusCode = StatusCodes.Status413PayloadTooLarge;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(new
            {
                error = new
                {
                    code = "file_too_large",
                    message = $"The uploaded file exceeds the maximum allowed size of {LiensUploadLimits.MaxMegabytes} MB."
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception. TraceId={TraceId}", context.TraceIdentifier);
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(new
            {
                error = new
                {
                    code = "server_error",
                    message = "An unexpected error occurred.",
                    traceId = context.TraceIdentifier
                }
            });
        }
    }

    private static bool IsMultipartBodyLimitError(InvalidDataException ex) =>
        ex.Message.Contains("Multipart body length limit", StringComparison.OrdinalIgnoreCase);
}
