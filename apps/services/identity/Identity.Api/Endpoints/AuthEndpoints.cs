using Identity.Application.DTOs;
using Identity.Application.Interfaces;

namespace Identity.Api.Endpoints;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this WebApplication app)
    {
        // ── POST /api/auth/login ─────────────────────────────────────────────
        // Anonymous. Validates credentials and returns AccessToken + session envelope.
        // The Next.js BFF receives this response, stores the token in an HttpOnly
        // cookie, and forwards only the session envelope (no raw token) to the browser.
        app.MapPost("/api/auth/login", async (
            LoginRequest request,
            IAuthService authService,
            CancellationToken ct) =>
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
        })
        .AllowAnonymous();

        // ── GET /api/auth/me ─────────────────────────────────────────────────
        // Authenticated (Bearer JWT required).
        // Returns the current session envelope derived from the validated JWT claims.
        // Called server-side by the Next.js BFF /api/auth/me route, which reads the
        // platform_session HttpOnly cookie and forwards it as Authorization: Bearer.
        // Never called directly from browser JS.
        app.MapGet("/api/auth/me", async (
            HttpContext httpContext,
            IAuthService authService,
            CancellationToken ct) =>
        {
            var response = await authService.GetCurrentUserAsync(httpContext.User, ct);
            return Results.Ok(response);
        })
        .RequireAuthorization();

        // ── POST /api/auth/logout ────────────────────────────────────────────
        // Anonymous (JWT may already be expired at logout time).
        // Backend is stateless — real logout is cookie deletion on the Next.js BFF.
        // Endpoint exists for forward-compatibility (refresh-token revocation, audit).
        app.MapPost("/api/auth/logout", (HttpContext _) => Results.NoContent())
            .AllowAnonymous();
    }
}
