using Intake.Application.Configuration;

namespace Intake.Api.Middleware;

public sealed class IntakeConfigurationExceptionMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (IntakeConfigurationException exception) when (!context.Response.HasStarted)
        {
            context.Response.StatusCode = exception.StatusCode;
            context.Response.ContentType = "application/problem+json";
            await context.Response.WriteAsJsonAsync(new
            {
                type = $"https://httpstatuses.com/{exception.StatusCode}",
                title = exception.Code,
                status = exception.StatusCode,
                detail = exception.Message,
                error = exception.Code,
                correlationId = context.GetCorrelationId(),
            });
        }
    }
}