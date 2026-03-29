using Documents.Domain.Interfaces;
using Documents.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace Documents.Api.Endpoints;

public static class HealthEndpoints
{
    public static void MapHealthEndpoints(this WebApplication app)
    {
        app.MapGet("/health", (IConfiguration cfg) =>
            Results.Ok(new
            {
                status    = "ok",
                service   = "documents-dotnet",
                timestamp = DateTime.UtcNow,
            }))
            .WithName("GetHealth")
            .WithTags("Health")
            .AllowAnonymous();

        app.MapGet("/health/ready", async (DocsDbContext db) =>
        {
            bool dbOk;
            try   { await db.Database.ExecuteSqlRawAsync("SELECT 1"); dbOk = true; }
            catch { dbOk = false; }

            var status = dbOk ? 200 : 503;
            return Results.Json(new
            {
                status    = dbOk ? "ready" : "degraded",
                checks    = new { database = dbOk ? "ok" : "fail" },
                timestamp = DateTime.UtcNow,
            }, statusCode: status);
        })
        .WithName("GetReadiness")
        .WithTags("Health")
        .AllowAnonymous();
    }
}
