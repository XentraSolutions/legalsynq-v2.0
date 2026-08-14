using Intake.Application;

namespace Intake.Api.Endpoints;

public static class InfoEndpoints
{
    public static void MapInfoEndpoints(this WebApplication app)
    {
        app.MapGet("/info", (IIntakeFoundationService service) =>
                Results.Ok(service.GetServiceInfo()))
            .AllowAnonymous()
            .WithTags("Health");
    }
}