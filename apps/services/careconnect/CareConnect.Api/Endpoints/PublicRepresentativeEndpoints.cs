using CareConnect.Api.Helpers;
using CareConnect.Application.DTOs;
using CareConnect.Application.Interfaces;

namespace CareConnect.Api.Endpoints;

/// <summary>
/// Anonymous Referral Representative Portal — no login, no JWT, no product role. Modeled
/// on PublicNetworkEndpoints' anonymous pattern (same two-layer trust boundary, same
/// AllowAnonymous + rate-limit shape) rather than the platform's authenticated-session
/// pattern, because the portal itself has none: the access code is the only credential,
/// end to end.
///
/// Unlike the public network directory (whose AccessCodeGate is a one-time, client-side-
/// only UX unlock — the data endpoints themselves stay open regardless), referral data is
/// PII (client name, DOB, phone, email) and every read here re-verifies the caller's code
/// server-side on every single request via IReferralAttributionAccessCodeService.VerifyAsync.
/// Nothing is cached and nothing is trusted from a prior request: revoking a code or
/// deactivating its attribution takes effect on the very next call, not on next login
/// (there is no login) and not on next page reload of a previously-unlocked client.
/// </summary>
public static class PublicRepresentativeEndpoints
{
    private const string SourceService = "public-representative";

    public static void MapPublicRepresentativeEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/public/representative");

        // ── POST /api/public/representative/verify ──────────────────────────
        // Stateless code check — tells the caller whether a code is currently valid and,
        // if so, which attribution it names (so the client can show "Cam Perry's referrals"
        // before the first data call). Never mutates anything.
        group.MapPost("/verify", async (
            VerifyReferralAttributionAccessCodeRequest request,
            HttpContext http,
            IConfiguration config,
            IReferralAttributionAccessCodeService codeService,
            CancellationToken ct) =>
        {
            var tenantId = PublicTrustBoundary.ValidateAndResolveTenantId(http, config, SourceService);
            if (tenantId is null)
                return Results.Problem(statusCode: StatusCodes.Status403Forbidden,
                    detail: "Request origin could not be verified.");

            var result = await codeService.VerifyAsync(tenantId.Value, request.Code, ct);
            return Results.Ok(result);
        }).AllowAnonymous().RequireRateLimiting("public-read-limit");

        // ── GET /api/public/representative/referrals ─────────────────────────
        group.MapGet("/referrals", async (
            [AsParameters] PublicRepresentativeReferralSearchParams p,
            HttpContext http,
            IConfiguration config,
            IReferralAttributionAccessCodeService codeService,
            IRepresentativeReferralService representativeService,
            CancellationToken ct) =>
        {
            var tenantId = PublicTrustBoundary.ValidateAndResolveTenantId(http, config, SourceService);
            if (tenantId is null)
                return Results.Problem(statusCode: StatusCodes.Status403Forbidden,
                    detail: "Request origin could not be verified.");

            var attributionId = await VerifyAndResolveAttributionAsync(codeService, tenantId.Value, p.Code, ct);
            if (attributionId is null)
                return Results.Json(new { error = "This code is invalid, has expired, or no longer grants access." }, statusCode: StatusCodes.Status401Unauthorized);

            var query = new GetRepresentativeReferralsQuery
            {
                SubmittedFrom = p.SubmittedFrom,
                SubmittedTo = p.SubmittedTo,
                Status = p.Status,
                ProviderId = p.ProviderId,
                LawFirmOrganizationId = p.LawFirmOrganizationId,
                Page = Math.Max(1, p.Page ?? 1),
                PageSize = Math.Clamp(p.PageSize ?? 20, 1, 100),
            };

            var result = await representativeService.SearchAsync(tenantId.Value, attributionId.Value, query, ct);
            return Results.Ok(result);
        }).AllowAnonymous().RequireRateLimiting("public-read-limit");

        // ── GET /api/public/representative/referrals/{referralId} ────────────
        group.MapGet("/referrals/{referralId:guid}", async (
            Guid referralId,
            string? code,
            HttpContext http,
            IConfiguration config,
            IReferralAttributionAccessCodeService codeService,
            IRepresentativeReferralService representativeService,
            CancellationToken ct) =>
        {
            var tenantId = PublicTrustBoundary.ValidateAndResolveTenantId(http, config, SourceService);
            if (tenantId is null)
                return Results.Problem(statusCode: StatusCodes.Status403Forbidden,
                    detail: "Request origin could not be verified.");

            var attributionId = await VerifyAndResolveAttributionAsync(codeService, tenantId.Value, code, ct);
            if (attributionId is null)
                return Results.Json(new { error = "This code is invalid, has expired, or no longer grants access." }, statusCode: StatusCodes.Status401Unauthorized);

            var detail = await representativeService.GetByIdAsync(tenantId.Value, attributionId.Value, referralId, ct);
            // Generic 404 for "doesn't exist" and "not attributed to this code" alike.
            return detail is null ? Results.NotFound() : Results.Ok(detail);
        }).AllowAnonymous().RequireRateLimiting("public-read-limit");

        // ── GET /api/public/representative/referral-metrics ─────────────────
        group.MapGet("/referral-metrics", async (
            string? code,
            DateTime? from,
            DateTime? to,
            HttpContext http,
            IConfiguration config,
            IReferralAttributionAccessCodeService codeService,
            IRepresentativeReferralService representativeService,
            CancellationToken ct) =>
        {
            var tenantId = PublicTrustBoundary.ValidateAndResolveTenantId(http, config, SourceService);
            if (tenantId is null)
                return Results.Problem(statusCode: StatusCodes.Status403Forbidden,
                    detail: "Request origin could not be verified.");

            var attributionId = await VerifyAndResolveAttributionAsync(codeService, tenantId.Value, code, ct);
            if (attributionId is null)
                return Results.Json(new { error = "This code is invalid, has expired, or no longer grants access." }, statusCode: StatusCodes.Status401Unauthorized);

            var metrics = await representativeService.GetMetricsAsync(tenantId.Value, attributionId.Value, from, to, ct);
            return Results.Ok(metrics);
        }).AllowAnonymous().RequireRateLimiting("public-read-limit");
    }

    /// <summary>
    /// The single choke point every data endpoint above calls through: re-verifies the raw
    /// code from scratch. Returns null for every failure mode (missing/malformed code,
    /// invalid/expired/revoked code, deactivated attribution) — callers surface one generic
    /// 401, never distinguishing which case occurred.
    /// </summary>
    private static async Task<Guid?> VerifyAndResolveAttributionAsync(
        IReferralAttributionAccessCodeService codeService,
        Guid tenantId,
        string? code,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(code))
            return null;

        var result = await codeService.VerifyAsync(tenantId, code, ct);
        return result.Ok ? result.ReferralAttributionId : null;
    }
}

public class PublicRepresentativeReferralSearchParams
{
    public string? Code { get; set; }
    public DateTime? SubmittedFrom { get; set; }
    public DateTime? SubmittedTo { get; set; }
    public string? Status { get; set; }
    public Guid? ProviderId { get; set; }
    public Guid? LawFirmOrganizationId { get; set; }
    public int? Page { get; set; }
    public int? PageSize { get; set; }
}
