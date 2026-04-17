using System.Net;
using System.Text.Json;
using Flow.Application.Exceptions;

namespace Flow.Api.Middleware;

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
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var (statusCode, response) = exception switch
        {
            NotFoundException notFound => (
                HttpStatusCode.NotFound,
                new ErrorResponse { Error = notFound.Message }
            ),
            ValidationException validation => (
                HttpStatusCode.BadRequest,
                new ErrorResponse { Error = "Validation failed.", Errors = validation.Errors }
            ),
            InvalidStateTransitionException transition => (
                HttpStatusCode.UnprocessableEntity,
                new ErrorResponse { Error = transition.Message }
            ),
            _ => (
                HttpStatusCode.InternalServerError,
                new ErrorResponse { Error = "An unexpected error occurred." }
            )
        };

        if (statusCode == HttpStatusCode.InternalServerError)
        {
            _logger.LogError(exception, "Unhandled exception");
        }

        context.Response.ContentType = "application/json";
        context.Response.StatusCode = (int)statusCode;

        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        };

        await context.Response.WriteAsync(JsonSerializer.Serialize(response, options));
    }
}

public class ErrorResponse
{
    public string Error { get; set; } = string.Empty;
    public IReadOnlyList<string>? Errors { get; set; }
}
