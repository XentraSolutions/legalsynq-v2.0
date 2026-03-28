using Identity.Application.DTOs;
using Identity.Application.Interfaces;

namespace Identity.Api.Endpoints;

public static class UserEndpoints
{
    public static void MapUserEndpoints(this WebApplication app)
    {
        app.MapPost("/api/users", async (CreateUserRequest request, IUserService userService, CancellationToken ct) =>
        {
            try
            {
                var user = await userService.CreateUserAsync(request, ct);
                return Results.Created($"/api/users/{user.Id}", user);
            }
            catch (InvalidOperationException ex)
            {
                return Results.Problem(ex.Message, statusCode: 400);
            }
        });

        app.MapGet("/api/users", async (IUserService userService, CancellationToken ct) =>
        {
            var users = await userService.GetAllAsync(ct);
            return Results.Ok(users);
        });

        app.MapGet("/api/users/{id:guid}", async (Guid id, IUserService userService, CancellationToken ct) =>
        {
            var user = await userService.GetByIdAsync(id, ct);
            return user is null ? Results.NotFound() : Results.Ok(user);
        });
    }
}
