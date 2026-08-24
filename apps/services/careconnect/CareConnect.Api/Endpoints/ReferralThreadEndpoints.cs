using CareConnect.Api.Helpers;
using CareConnect.Api.Options;
using CareConnect.Application.DTOs;
using CareConnect.Application.Interfaces;
using Microsoft.Extensions.Options;

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
            var result = await threadService.GetPublicThreadAccessAsync(token, ct);
            if (result.Data is not null)
                return Results.Ok(result.Data);

            if (result.FailureReason == CareConnect.Application.DTOs.ReferralTokenFailureReasons.ReferralNotFound)
                return Results.Json(new { reason = result.FailureReason }, statusCode: StatusCodes.Status404NotFound);

            return Results.Json(new { reason = result.FailureReason }, statusCode: StatusCodes.Status401Unauthorized);
        }).AllowAnonymous().RequireRateLimiting("public-read-limit");

        // ── POST /api/public/referrals/thread/comments?token=... ────────────
        // Adds a comment and emails the other party.
        app.MapPost("/api/public/referrals/thread/comments", async (
            string              token,
            HttpRequest         httpRequest,
            IReferralThreadService threadService,
            IOptions<AttachmentUploadOptions> uploadOptions,
            CancellationToken   ct) =>
        {
            var req = await ReadPostCommentRequestAsync(httpRequest, ct);
            if (req is null)
                return Results.BadRequest(new { error = "Request body is required." });

            if (string.IsNullOrWhiteSpace(req.SenderType) ||
                (req.SenderType != "referrer" && req.SenderType != "provider"))
                return Results.BadRequest(new { error = "senderType must be 'referrer' or 'provider'." });

            var files = req.Files ?? new FormFileCollection();
            var message = MessageAttachmentUploadHelper.NormalizeMessage(req.Message);
            var messageValidationError = MessageAttachmentUploadHelper.ValidateMessage(message, files);
            if (messageValidationError is not null)
                return messageValidationError;

            var validationError = MessageAttachmentUploadHelper.ValidateFiles(files, uploadOptions.Value);
            if (validationError is not null)
                return validationError;

            List<ReferralMessageAttachmentUpload> uploads = [];
            try
            {
                uploads = MessageAttachmentUploadHelper.OpenUploads(files);
                var comment = uploads.Count == 0
                    ? await threadService.PostPublicCommentAsync(
                        token,
                        req.SenderType,
                        message,
                        ct)
                    : await threadService.PostPublicCommentWithAttachmentsAsync(
                        token,
                        req.SenderType,
                        message,
                        uploads,
                        ct);

                if (comment is null)
                    return Results.Problem(statusCode: 404, detail: "Token is invalid or expired.");

                return Results.Created($"/api/public/referrals/thread/comments/{comment.Id}", comment);
            }
            finally
            {
                MessageAttachmentUploadHelper.DisposeUploads(uploads);
            }
        })
        .AllowAnonymous()
        .RequireRateLimiting("public-referral-limit")
        .DisableAntiforgery();
    }

    private static async Task<PostCommentRequest?> ReadPostCommentRequestAsync(
        HttpRequest httpRequest,
        CancellationToken ct)
    {
        if (httpRequest.HasFormContentType)
        {
            var form = await httpRequest.ReadFormAsync(ct);
            return new PostCommentRequest(
                form["senderType"].FirstOrDefault() ?? string.Empty,
                form["message"].FirstOrDefault() ?? string.Empty,
                form.Files);
        }

        var req = await httpRequest.ReadFromJsonAsync<PostCommentJsonRequest>(cancellationToken: ct);
        return req is null
            ? null
            : new PostCommentRequest(req.SenderType, req.Message, null);
    }

    private sealed record PostCommentJsonRequest(string SenderType, string Message);
    private sealed record PostCommentRequest(string SenderType, string Message, IFormFileCollection? Files);
}
