using System.Security.Claims;
using Identity.Application;
using Identity.Application.DTOs;
using Identity.Application.Interfaces;
using Identity.Domain;
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
            AcceptInviteRequest body,
            IAuthService        authService,
            CancellationToken   ct) =>
        {
            if (string.IsNullOrWhiteSpace(body.Token))
                return Results.BadRequest(new { error = "token is required." });
            if (string.IsNullOrWhiteSpace(body.NewPassword) || body.NewPassword.Length < 8)
                return Results.BadRequest(new { error = "newPassword must be at least 8 characters." });

            try
            {
                await authService.AcceptInviteAsync(body.Token, body.NewPassword, ct);
                return Results.Ok(new { message = "Invitation accepted. Your account is now active." });
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        })
        .AllowAnonymous();

        // ── POST /api/auth/change-password ───────────────────────────────────
        // Authenticated. Verifies the caller's current password then replaces it.
        //
        // Flow:
        //   1. Extract user id from the validated JWT (sub claim).
        //   2. Validate request body (currentPassword, newPassword length ≥ 8).
        //   3. Load the user record from the database.
        //   4. Verify currentPassword against the stored bcrypt hash.
        //   5. Hash the new password and call user.SetPassword().
        //   6. Persist changes and emit identity.user.password_changed audit event.
        app.MapPost("/api/auth/change-password", async (
            ChangePasswordRequest body,
            HttpContext           httpContext,
            IAuthService         authService,
            CancellationToken    ct) =>
        {
            var userIdStr = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier)
                         ?? httpContext.User.FindFirstValue("sub");

            if (!Guid.TryParse(userIdStr, out var userId))
                return Results.Problem("Invalid or missing user identity.", statusCode: 401);

            if (string.IsNullOrWhiteSpace(body.CurrentPassword))
                return Results.BadRequest(new { error = "currentPassword is required." });

            if (string.IsNullOrWhiteSpace(body.NewPassword) || body.NewPassword.Length < 8)
                return Results.BadRequest(new { error = "newPassword must be at least 8 characters." });

            if (body.CurrentPassword == body.NewPassword)
                return Results.BadRequest(new { error = "New password must differ from the current password." });

            var ipAddress = httpContext.Connection.RemoteIpAddress?.ToString();
            try
            {
                await authService.ChangePasswordAsync(userId, body.CurrentPassword, body.NewPassword, ipAddress, ct);
                return Results.Ok(new { message = "Password changed successfully." });
            }
            catch (InvalidOperationException ex)
            {
                return ex.Message.Contains("not found")
                    ? Results.Problem(ex.Message, statusCode: 404)
                    : Results.Problem(ex.Message, statusCode: 400);
            }
        })
        .RequireAuthorization();

        // ── PATCH /api/profile/avatar ─────────────────────────────────────────
        // Authenticated. Stores the document ID of an already-uploaded avatar.
        // The actual file upload goes directly to the documents service via BFF.
        app.MapPatch("/api/profile/avatar", async (
            SetAvatarRequest   body,
            HttpContext        httpContext,
            IUserRepository    userRepo,
            IAuditEventClient  auditClient,
            CancellationToken  ct) =>
        {
            var userIdStr = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier)
                         ?? httpContext.User.FindFirstValue("sub");

            if (!Guid.TryParse(userIdStr, out var userId))
                return Results.Problem("Invalid or missing user identity.", statusCode: 401);

            if (!Guid.TryParse(body.DocumentId, out var documentId))
                return Results.BadRequest(new { error = "documentId must be a valid UUID." });

            await userRepo.UpdateAvatarAsync(userId, documentId, ct);

            var now = DateTimeOffset.UtcNow;
            _ = auditClient.IngestAsync(new IngestAuditEventRequest
            {
                EventType     = "identity.user.avatar_set",
                EventCategory = EventCategory.DataChange,
                SourceSystem  = "identity-service",
                SourceService = "auth-api",
                Visibility    = VisibilityScope.Tenant,
                Severity      = SeverityLevel.Info,
                OccurredAtUtc = now,
                Scope         = new AuditEventScopeDto { ScopeType = ScopeType.Tenant },
                Actor         = new AuditEventActorDto
                {
                    Id        = userIdStr,
                    Type      = ActorType.User,
                    IpAddress = httpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault()?.Split(',')[0].Trim()
                             ?? httpContext.Connection.RemoteIpAddress?.ToString(),
                },
                Entity         = new AuditEventEntityDto { Type = "User", Id = userIdStr! },
                Action         = "AvatarSet",
                Description    = $"User updated their profile picture (document {documentId}).",
                IdempotencyKey = LegalSynq.AuditClient.IdempotencyKey.ForWithTimestamp(
                    now, "identity-service", "identity.user.avatar_set", userIdStr ?? ""),
                Tags = ["profile", "avatar"],
            });

            return Results.Ok(new { avatarDocumentId = documentId });
        })
        .RequireAuthorization();

        // ── DELETE /api/profile/avatar ────────────────────────────────────────
        // Authenticated. Clears the user's avatar document reference.
        app.MapDelete("/api/profile/avatar", async (
            HttpContext        httpContext,
            IUserRepository    userRepo,
            IAuditEventClient  auditClient,
            CancellationToken  ct) =>
        {
            var userIdStr = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier)
                         ?? httpContext.User.FindFirstValue("sub");

            if (!Guid.TryParse(userIdStr, out var userId))
                return Results.Problem("Invalid or missing user identity.", statusCode: 401);

            await userRepo.UpdateAvatarAsync(userId, null, ct);

            var now = DateTimeOffset.UtcNow;
            _ = auditClient.IngestAsync(new IngestAuditEventRequest
            {
                EventType     = "identity.user.avatar_removed",
                EventCategory = EventCategory.DataChange,
                SourceSystem  = "identity-service",
                SourceService = "auth-api",
                Visibility    = VisibilityScope.Tenant,
                Severity      = SeverityLevel.Info,
                OccurredAtUtc = now,
                Scope         = new AuditEventScopeDto { ScopeType = ScopeType.Tenant },
                Actor         = new AuditEventActorDto
                {
                    Id        = userIdStr,
                    Type      = ActorType.User,
                    IpAddress = httpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault()?.Split(',')[0].Trim()
                             ?? httpContext.Connection.RemoteIpAddress?.ToString(),
                },
                Entity         = new AuditEventEntityDto { Type = "User", Id = userIdStr! },
                Action         = "AvatarRemoved",
                Description    = "User removed their profile picture.",
                IdempotencyKey = LegalSynq.AuditClient.IdempotencyKey.ForWithTimestamp(
                    now, "identity-service", "identity.user.avatar_removed", userIdStr ?? ""),
                Tags = ["profile", "avatar"],
            });

            return Results.NoContent();
        })
        .RequireAuthorization();

        // ── POST /api/auth/password-reset/confirm ─────────────────────────────
        // Anonymous. Accepts a password-reset token (admin-triggered), validates it,
        // sets a new password, and invalidates all existing sessions (SessionVersion++).
        //
        // Flow:
        //   1. Hash the raw token with SHA-256.
        //   2. Look up the PasswordResetToken by hash.
        //   3. Validate: status == PENDING and not expired.
        //   4. Set the user's new password (User.SetPassword increments SessionVersion).
        //   5. Mark the token as used.
        //   6. Emit identity.user.password_reset_completed audit event.
        app.MapPost("/api/auth/password-reset/confirm", async (
            PasswordResetConfirmRequest body,
            IAuthService                authService,
            CancellationToken           ct) =>
        {
            if (string.IsNullOrWhiteSpace(body.Token))
                return Results.BadRequest(new { error = "token is required." });
            if (string.IsNullOrWhiteSpace(body.NewPassword) || body.NewPassword.Length < 8)
                return Results.BadRequest(new { error = "newPassword must be at least 8 characters." });

            try
            {
                await authService.ConfirmPasswordResetAsync(body.Token, body.NewPassword, ct);
                return Results.Ok(new { message = "Password updated successfully." });
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { error = ex.Message });
            }
        })
        .AllowAnonymous();

        // ── POST /api/auth/forgot-password ──────────────────────────────────
        // Anonymous. Self-service password reset request.
        // Accepts { tenantCode, email }, validates the user exists, generates a
        // reset token, and returns the raw token in the response.
        // Future: the raw token will be emailed instead of returned in the response.
        app.MapPost("/api/auth/forgot-password", async (
            ForgotPasswordRequest body,
            IAuthService          authService,
            CancellationToken     ct) =>
        {
            if (string.IsNullOrWhiteSpace(body.TenantCode))
                return Results.BadRequest(new { error = "tenantCode is required." });
            if (string.IsNullOrWhiteSpace(body.Email))
                return Results.BadRequest(new { error = "email is required." });

            var rawToken = await authService.ForgotPasswordAsync(body.TenantCode, body.Email, ct);

            // Always return a generic message to avoid user enumeration.
            return Results.Ok(new
            {
                message = "If an account exists with that email, a password reset link has been generated.",
                resetToken = rawToken,
            });
        })
        .AllowAnonymous();
    }

    private record AcceptInviteRequest(string Token, string NewPassword);
    private record PasswordResetConfirmRequest(string Token, string NewPassword);
    private record ChangePasswordRequest(string CurrentPassword, string NewPassword);
    private record SetAvatarRequest(string DocumentId);
    private record ForgotPasswordRequest(string TenantCode, string Email);
}
