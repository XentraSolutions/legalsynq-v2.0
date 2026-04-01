using System.Security.Claims;
using Identity.Application.DTOs;
using Identity.Application.Interfaces;
using Identity.Infrastructure.Data;
using LegalSynq.AuditClient;
using LegalSynq.AuditClient.DTOs;
using LegalSynq.AuditClient.Enums;
using Microsoft.EntityFrameworkCore;

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

        // ── POST /api/auth/accept-invite ─────────────────────────────────────
        // Anonymous. Accepts an invitation token, sets a new password, and
        // activates the invited user account.
        //
        // Flow:
        //   1. Hash the raw token with SHA-256.
        //   2. Look up the UserInvitation by token hash.
        //   3. Validate: status == PENDING and not expired.
        //   4. Set the user's password and mark them active.
        //   5. Mark the invitation accepted.
        //   6. Emit identity.user.invite_accepted audit event.
        app.MapPost("/api/auth/accept-invite", async (
            AcceptInviteRequest   body,
            IdentityDbContext     db,
            IPasswordHasher       passwordHasher,
            IAuditEventClient     auditClient,
            CancellationToken     ct) =>
        {
            if (string.IsNullOrWhiteSpace(body.Token))
                return Results.BadRequest(new { error = "token is required." });
            if (string.IsNullOrWhiteSpace(body.NewPassword) || body.NewPassword.Length < 8)
                return Results.BadRequest(new { error = "newPassword must be at least 8 characters." });

            // Hash the raw token the same way InviteUser stored it.
            var tokenHash = Convert.ToHexString(
                System.Security.Cryptography.SHA256.HashData(
                    System.Text.Encoding.UTF8.GetBytes(body.Token)));

            var invitation = await db.UserInvitations
                .Include(i => i.User)
                .FirstOrDefaultAsync(i => i.TokenHash == tokenHash, ct);

            if (invitation is null)
                return Results.BadRequest(new { error = "Invalid or expired invitation token." });

            if (invitation.Status != Identity.Domain.UserInvitation.Statuses.Pending)
                return Results.BadRequest(new
                {
                    error = invitation.Status == Identity.Domain.UserInvitation.Statuses.Accepted
                        ? "This invitation has already been accepted."
                        : "This invitation is no longer valid.",
                });

            if (invitation.IsExpired())
                return Results.BadRequest(new { error = "This invitation has expired. Please request a new one." });

            var user = invitation.User;
            if (user is null)
                return Results.Problem("User record not found for this invitation.", statusCode: 500);

            // Set the new password and activate the account.
            var passwordHash = passwordHasher.Hash(body.NewPassword);
            user.SetPassword(passwordHash);
            user.Activate();

            // Mark the invitation accepted.
            invitation.Accept();

            await db.SaveChangesAsync(ct);

            // Emit audit event (fire-and-observe).
            var now = DateTimeOffset.UtcNow;
            _ = auditClient.IngestAsync(new IngestAuditEventRequest
            {
                EventType     = "identity.user.invite_accepted",
                EventCategory = EventCategory.Administrative,
                SourceSystem  = "identity-service",
                SourceService = "auth-api",
                Visibility    = VisibilityScope.Tenant,
                Severity      = SeverityLevel.Info,
                OccurredAtUtc = now,
                Scope = new AuditEventScopeDto
                {
                    ScopeType = ScopeType.Tenant,
                    TenantId  = user.TenantId.ToString(),
                },
                Actor       = new AuditEventActorDto { Type = ActorType.User, Id = user.Id.ToString() },
                Entity      = new AuditEventEntityDto { Type = "User", Id = user.Id.ToString() },
                Action      = "InviteAccepted",
                Description = $"User '{user.Email}' accepted invitation and activated account in tenant {user.TenantId}.",
                IdempotencyKey = LegalSynq.AuditClient.IdempotencyKey.For(
                    "identity-service", "identity.user.invite_accepted", invitation.Id.ToString()),
                Tags = ["user-management", "invite", "activation"],
            });

            return Results.Ok(new { message = "Invitation accepted. Your account is now active." });
        })
        .AllowAnonymous();
    }

    private record AcceptInviteRequest(string Token, string NewPassword);
}
