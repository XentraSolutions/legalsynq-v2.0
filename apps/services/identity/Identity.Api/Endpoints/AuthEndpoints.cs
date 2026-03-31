using System.Security.Claims;
using Identity.Application.DTOs;
using Identity.Application.Interfaces;
using LegalSynq.AuditClient;
using LegalSynq.AuditClient.DTOs;
using LegalSynq.AuditClient.Enums;

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
            HttpContext httpContext,
            IAuthService authService,
            CancellationToken ct) =>
        {
            try
            {
                var ip = httpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault()?.Split(',')[0].Trim()
                      ?? httpContext.Connection.RemoteIpAddress?.ToString();
                var response = await authService.LoginAsync(request, ip, ct);
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
        // Emits identity.user.logout for HIPAA audit trail completeness.
        app.MapPost("/api/auth/logout", (
            HttpContext       httpContext,
            IAuditEventClient auditClient) =>
        {
            // Extract identity from the JWT claim if still present in the request.
            // The token may be expired — we read claims without re-validating the signature.
            var principal  = httpContext.User;
            var userId     = principal.FindFirstValue(ClaimTypes.NameIdentifier)
                          ?? principal.FindFirstValue("sub");
            var tenantId   = principal.FindFirstValue("tenant_id");
            var email      = principal.FindFirstValue(ClaimTypes.Email)
                          ?? principal.FindFirstValue("email");
            var name       = principal.FindFirstValue(ClaimTypes.Name)
                          ?? principal.FindFirstValue("name")
                          ?? email;

            // Fire-and-observe: emit audit event without gating the logout response.
            var now = DateTimeOffset.UtcNow;
            _ = auditClient.IngestAsync(new IngestAuditEventRequest
            {
                EventType     = "identity.user.logout",
                EventCategory = EventCategory.Security,
                SourceSystem  = "identity-service",
                SourceService = "auth-api",
                Visibility    = VisibilityScope.Tenant,
                Severity      = SeverityLevel.Info,
                OccurredAtUtc = now,
                Scope = new AuditEventScopeDto
                {
                    ScopeType = ScopeType.Tenant,
                    TenantId  = tenantId,
                },
                Actor = new AuditEventActorDto
                {
                    Id        = userId,
                    Type      = ActorType.User,
                    Name      = name,
                    IpAddress = httpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault()?.Split(',')[0].Trim()
                             ?? httpContext.Connection.RemoteIpAddress?.ToString(),
                },
                Entity      = userId is not null ? new AuditEventEntityDto { Type = "User", Id = userId } : null,
                Action      = "Logout",
                Description = $"User '{email ?? userId ?? "unknown"}' logged out.",
                IdempotencyKey = IdempotencyKey.ForWithTimestamp(now, "identity-service", "identity.user.logout", userId ?? email ?? "anonymous"),
                Tags = ["auth", "logout", "session"],
            });

            return Results.NoContent();
        })
        .AllowAnonymous();
    }
}
