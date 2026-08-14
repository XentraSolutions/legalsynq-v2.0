using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using System.Text.Json;

namespace Intake.Api.Endpoints;

public static class HealthEndpoints
{
    public static void MapHealthEndpoints(this WebApplication app)
    {
        app.MapHealthChecks("/health", new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains("live"),
            ResponseWriter = WriteHealthJson,
        }).AllowAnonymous().WithTags("Health");

        app.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains("ready"),
            ResponseWriter = WriteHealthJson,
        }).AllowAnonymous().WithTags("Health");
    }

    private static async Task WriteHealthJson(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json";
        context.Response.StatusCode = report.Status == HealthStatus.Unhealthy ? 503 : 200;

        var result = new
        {
            status = report.Status.ToString().ToLowerInvariant(),
            service = "intake",
            timestamp = DateTimeOffset.UtcNow,
            totalDuration = report.TotalDuration.TotalMilliseconds,
            checks = report.Entries.Select(entry => new
            {
                name = entry.Key,
                status = entry.Value.Status.ToString().ToLowerInvariant(),
                description = entry.Value.Description,
                duration = entry.Value.Duration.TotalMilliseconds,
                tags = entry.Value.Tags,
            }),
        };

        await context.Response.WriteAsync(JsonSerializer.Serialize(
            result,
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));
    }
}