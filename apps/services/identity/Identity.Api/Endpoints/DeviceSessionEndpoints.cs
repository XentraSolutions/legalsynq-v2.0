using System.Security.Claims;
using Identity.Application.DTOs;
using Identity.Application.Errors;
using Identity.Application.Interfaces;
using Identity.Infrastructure.Services;
using Microsoft.Extensions.Options;

namespace Identity.Api.Endpoints;

/// <summary>
/// BE-BIO-011/012/013/014/015/016: biometric-enable/disable toggles and
/// device-session management (list/revoke/logout-all). The refresh and v1
/// logout endpoints live in AuthEndpoints.cs instead, since they are
/// conceptually part of the auth lifecycle and must be anonymous (the access
/// token may be expired at call time), unlike everything here which requires
/// an authenticated caller.
/// </summary>
public static class DeviceSessionEndpoints
{
    public static void MapDeviceSessionEndpoints(this WebApplication app)
    {
        // ── GET /api/v1/auth/device-sessions ────────────────────────────────────
        // BE-BIO-015. Never includes token material.
        app.MapGet("/api/v1/auth/device-sessions", async (
            HttpContext            httpContext,
            IDeviceSessionService  deviceSessionService,
            CancellationToken      ct) =>
        {
            if (!TryGetUserId(httpContext, out var userId))
                return Results.Unauthorized();

            var currentDeviceSessionId = TryGetDeviceSessionIdClaim(httpContext, out var claimed) ? claimed : (Guid?)null;

            var sessions = await deviceSessionService.ListSessionsAsync(userId, currentDeviceSessionId, ct);
            return Results.Ok(sessions);
        })
        .RequireAuthorization()
        .RequireRateLimiting("auth-device-session-list");

        // ── DELETE /api/v1/auth/device-sessions/{id} ────────────────────────────
        // BE-BIO-016. IDOR-checked (service scopes the lookup to the caller's own
        // UserId). SEC-014: revoking a device OTHER than the caller's own current
        // one is the higher-risk action and requires step-up; revoking your own
        // current session needs none, since you're already using it.
        app.MapDelete("/api/v1/auth/device-sessions/{id:guid}", async (
            Guid                    id,
            HttpContext             httpContext,
            IDeviceSessionService   deviceSessionService,
            IOptions<RefreshTokenPolicyOptions> policyOptions,
            CancellationToken       ct) =>
        {
            if (!TryGetUserId(httpContext, out var userId))
                return Results.Unauthorized();

            var currentDeviceSessionId = TryGetDeviceSessionIdClaim(httpContext, out var claimed) ? claimed : (Guid?)null;
            var isOwnCurrentSession = currentDeviceSessionId.HasValue && currentDeviceSessionId.Value == id;

            if (!isOwnCurrentSession)
            {
                var stepUpFailure = await CheckStepUpAsync(httpContext, userId, deviceSessionService, policyOptions.Value, ct);
                if (stepUpFailure is not null) return stepUpFailure;
            }

            var found = await deviceSessionService.RevokeSessionAsync(userId, id, "UserRevoked", ct);
            return found ? Results.NoContent() : ProblemForErrorCode(AuthErrorCodes.DeviceSessionNotFound);
        })
        .RequireAuthorization()
        .RequireRateLimiting("auth-device-session-revoke");

        // ── POST /api/v1/auth/logout-all ────────────────────────────────────────
        // BE-BIO-014. Always requires step-up (recent primary auth on the calling
        // device), since it revokes every device including the one making the call.
        app.MapPost("/api/v1/auth/logout-all", async (
            HttpContext             httpContext,
            IDeviceSessionService   deviceSessionService,
            IOptions<RefreshTokenPolicyOptions> policyOptions,
            CancellationToken       ct) =>
        {
            if (!TryGetUserId(httpContext, out var userId))
                return Results.Unauthorized();

            var stepUpFailure = await CheckStepUpAsync(httpContext, userId, deviceSessionService, policyOptions.Value, ct);
            if (stepUpFailure is not null) return stepUpFailure;

            var count = await deviceSessionService.LogoutAllAsync(userId, ct);
            return Results.Ok(new { revokedCount = count });
        })
        .RequireAuthorization()
        .RequireRateLimiting("auth-logout-all");

        // ── POST /api/v1/auth/device-sessions/{id}/biometric/enable ─────────────
        // BE-BIO-011. Administrative flag only — never itself proof that biometric
        // authentication occurred for any specific request (SEC-006).
        app.MapPost("/api/v1/auth/device-sessions/{id:guid}/biometric/enable", async (
            Guid                    id,
            HttpContext             httpContext,
            IDeviceSessionService   deviceSessionService,
            CancellationToken       ct) =>
        {
            if (!TryGetUserId(httpContext, out var userId))
                return Results.Unauthorized();

            var found = await deviceSessionService.EnableBiometricAsync(userId, id, ct);
            return found ? Results.NoContent() : ProblemForErrorCode(AuthErrorCodes.DeviceSessionNotFound);
        })
        .RequireAuthorization()
        .RequireRateLimiting("auth-biometric-toggle");

        // ── POST /api/v1/auth/device-sessions/{id}/biometric/disable ────────────
        // BE-BIO-012. Disables the flag and revokes the session/token together.
        // Idempotent (BE-BIO-019).
        app.MapPost("/api/v1/auth/device-sessions/{id:guid}/biometric/disable", async (
            Guid                    id,
            HttpContext             httpContext,
            IDeviceSessionService   deviceSessionService,
            CancellationToken       ct) =>
        {
            if (!TryGetUserId(httpContext, out var userId))
                return Results.Unauthorized();

            var found = await deviceSessionService.DisableBiometricAsync(userId, id, ct);
            return found ? Results.NoContent() : ProblemForErrorCode(AuthErrorCodes.DeviceSessionNotFound);
        })
        .RequireAuthorization()
        .RequireRateLimiting("auth-biometric-toggle");
    }

    private static bool TryGetUserId(HttpContext httpContext, out Guid userId)
    {
        var sub = httpContext.User.FindFirstValue("sub")
               ?? httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(sub, out userId);
    }

    private static bool TryGetDeviceSessionIdClaim(HttpContext httpContext, out Guid deviceSessionId)
    {
        var claim = httpContext.User.FindFirstValue("device_session_id");
        return Guid.TryParse(claim, out deviceSessionId);
    }

    /// <summary>
    /// SEC-014: step-up gate — requires DeviceSession.LastSuccessfulAuthAtUtc for
    /// the CALLING device session to be within the configured window. The calling
    /// session is resolved from the `device_session_id` JWT claim (present only on
    /// tokens minted by GenerateRefreshedAccessToken — i.e. tokens obtained via a
    /// biometric-gated refresh). Absent that claim, step-up cannot be satisfied and
    /// the caller must complete primary authentication again.
    /// </summary>
    private static async Task<IResult?> CheckStepUpAsync(
        HttpContext httpContext,
        Guid userId,
        IDeviceSessionService deviceSessionService,
        RefreshTokenPolicyOptions policy,
        CancellationToken ct)
    {
        if (!TryGetDeviceSessionIdClaim(httpContext, out var deviceSessionId))
            return ProblemForErrorCode(AuthErrorCodes.SessionReauthenticationRequired);

        var lastAuth = await deviceSessionService.GetLastSuccessfulAuthAsync(userId, deviceSessionId, ct);
        if (lastAuth is null)
            return ProblemForErrorCode(AuthErrorCodes.SessionReauthenticationRequired);

        var age = DateTime.UtcNow - lastAuth.Value;
        if (age > TimeSpan.FromMinutes(policy.StepUpWindowMinutes))
            return ProblemForErrorCode(AuthErrorCodes.SessionReauthenticationRequired);

        return null;
    }

    private static IResult ProblemForErrorCode(string errorCode)
    {
        var (statusCode, detail) = errorCode switch
        {
            AuthErrorCodes.DeviceSessionNotFound => (404, "The requested device session was not found."),
            AuthErrorCodes.SessionReauthenticationRequired => (403, "Please sign in again to continue."),
            _ => (400, "The request could not be completed."),
        };

        return Results.Problem(
            detail: detail,
            statusCode: statusCode,
            extensions: new Dictionary<string, object?> { ["errorCode"] = errorCode });
    }
}
