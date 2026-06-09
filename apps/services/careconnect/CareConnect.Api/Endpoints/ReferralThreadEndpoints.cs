using CareConnect.Application.Interfaces;

namespace CareConnect.Api.Endpoints;

/// <summary>
/// Public referral thread — token-authenticated, no login required.
/// Both the law firm (referrer) and the provider use the same HMAC-signed view token
/// to access referral status and post comments. The token IS the authentication.
/// Route: /api/public/referrals/thread   (proxied via gateway as /careconnect/api/public/referrals/thread)
/// </summary>
public static class ReferralThreadEndpoints
{
    public static void MapReferralThreadEndpoints(this WebApplication app)
    {
        // ── GET /api/public/referrals/thread?token=... ──────────────────────
        // Returns referral summary + comment thread for the given token.
        app.MapGet("/api/public/referrals/thread", async (
            string              token,
            IReferralThreadService threadService,
            CancellationToken   ct) =>
        {
            var thread = await threadService.GetPublicThreadAsync(token, ct);
            if (thread is null)
                return Results.Problem(statusCode: 404, detail: "Token is invalid or expired.");
            return Results.Ok(thread);
        }).AllowAnonymous().RequireRateLimiting("public-read-limit");

        // ── POST /api/public/referrals/thread/comments?token=... ────────────
        // Adds a comment and emails the other party.
        app.MapPost("/api/public/referrals/thread/comments", async (
            string              token,
            PostCommentRequest  req,
            IReferralThreadService threadService,
            CancellationToken   ct) =>
        {
            if (string.IsNullOrWhiteSpace(req.SenderType) ||
                (req.SenderType != "referrer" && req.SenderType != "provider"))
                return Results.BadRequest(new { error = "senderType must be 'referrer' or 'provider'." });

            if (string.IsNullOrWhiteSpace(req.SenderName) || req.SenderName.Length > 200)
                return Results.BadRequest(new { error = "senderName is required and must be 200 characters or fewer." });

            if (string.IsNullOrWhiteSpace(req.Message) || req.Message.Length > 4000)
                return Results.BadRequest(new { error = "message is required and must be 4000 characters or fewer." });

            var comment = await threadService.PostPublicCommentAsync(
                token,
                req.SenderType,
                req.SenderName,
                req.Message,
                ct);
            if (comment is null)
                return Results.Problem(statusCode: 404, detail: "Token is invalid or expired.");

            return Results.Created($"/api/public/referrals/thread/comments/{comment.Id}", new
            {
                comment.Id,
                comment.SenderType,
                comment.SenderName,
                comment.Message,
                comment.CreatedAt,
            });
        }).AllowAnonymous().RequireRateLimiting("public-referral-limit");
    }

    private sealed record PostCommentRequest(string SenderType, string SenderName, string Message);
}
