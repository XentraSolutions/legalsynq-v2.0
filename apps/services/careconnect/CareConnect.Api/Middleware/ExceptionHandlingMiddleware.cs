using BuildingBlocks.Exceptions;
using System.Security.Claims;
using System.Text.Json;

namespace CareConnect.Api.Middleware;

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

            var response = new
            {
                error = new
                {
                    code = "VALIDATION_ERROR",
                    message = "One or more validation errors occurred.",
                    details = ex.Errors
                }
            };

            await context.Response.WriteAsync(JsonSerializer.Serialize(response, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            }));
        }
        catch (NotFoundException ex)
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            context.Response.ContentType = "application/json";

            var response = new
            {
                error = new
                {
                    code = "NOT_FOUND",
                    message = ex.Message
                }
            };

            await context.Response.WriteAsync(JsonSerializer.Serialize(response, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            }));
        }
        catch (ProductAccessDeniedException pex)
        {
            _logger.LogWarning(
                "Product authorization denied: code={ErrorCode} product={ProductCode} org={OrgId} user={User} path={Path}",
                pex.ErrorCode, pex.ProductCode, pex.OrganizationId,
                context.User.FindFirst("sub")?.Value, context.Request.Path);

            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            context.Response.ContentType = "application/json";

            var response = new
            {
                error = new
                {
                    code = pex.ErrorCode,
                    message = pex.DenialReason ?? pex.Message,
                    productCode = pex.ProductCode,
                    requiredRoles = pex.RequiredRoles,
                    organizationId = pex.OrganizationId
                }
            };

            await context.Response.WriteAsync(JsonSerializer.Serialize(response, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            }));
        }
        catch (ForbiddenException ex)
        {
            _logger.LogWarning("Forbidden: {Message}", ex.Message);

            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            context.Response.ContentType = "application/json";

            var response = new
            {
                error = new
                {
                    code = "FORBIDDEN",
                    message = ex.Message
                }
            };

            await context.Response.WriteAsync(JsonSerializer.Serialize(response, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            }));
        }
        catch (ConflictException ex)
        {
            context.Response.StatusCode = StatusCodes.Status409Conflict;
            context.Response.ContentType = "application/json";

            var response = new
            {
                error = new
                {
                    code = ex.ErrorCode ?? "CONFLICT",
                    message = ex.Message
                }
            };

            await context.Response.WriteAsync(JsonSerializer.Serialize(response, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            }));
        }
        catch (BadHttpRequestException)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            context.Response.ContentType = "application/json";

            var response = new
            {
                error = new
                {
                    code = "BAD_REQUEST",
                    message = "The request body is invalid or malformed."
                }
            };

            await context.Response.WriteAsync(JsonSerializer.Serialize(response, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            }));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception: {Message}", ex.Message);

            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            context.Response.ContentType = "application/json";

            var response = new
            {
                error = new
                {
                    code = "INTERNAL_ERROR",
                    message = "An unexpected error occurred. Please try again later."
                }
            };

            await context.Response.WriteAsync(JsonSerializer.Serialize(response, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            }));
        }
    }
}
