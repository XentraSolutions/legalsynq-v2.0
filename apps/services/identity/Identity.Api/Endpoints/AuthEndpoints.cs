using Identity.Application.DTOs;
using Identity.Application.Interfaces;

namespace Identity.Api.Endpoints;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this WebApplication app)
    {
        app.MapPost("/api/auth/login", async (LoginRequest request, IAuthService authService, CancellationToken ct) =>
        {
            try
            {
                var response = await authService.LoginAsync(request, ct);
                return Results.Ok(response);
            }
            catch (UnauthorizedAccessException)
            {
                return Results.Problem("Invalid credentials.", statusCode: 401);
            }
            catch (InvalidOperationException ex)
            {
                return Results.Problem(ex.Message, statusCode: 400);
            }
        });
    }
}
