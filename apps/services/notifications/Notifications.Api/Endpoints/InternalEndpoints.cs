using Notifications.Application.DTOs;
using Notifications.Infrastructure.Services;

namespace Notifications.Api.Endpoints;

public static class InternalEndpoints
{
    public static void MapInternalEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/internal").WithTags("Internal");

        group.MapPost("/send-email", async (InternalEmailService service, InternalSendEmailDto request) =>
        {
            var result = await service.SendAsync(request);
            return result.Success ? Results.Ok(result) : Results.Json(result, statusCode: 502);
        });
    }
}
