using BuildingBlocks.Exceptions;
using CareConnect.Application.DTOs;
using CareConnect.Application.Interfaces;
using CareConnect.Application.Repositories;
using CareConnect.Domain;
using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;

namespace CareConnect.Api.Endpoints;

// CC2-INT-B07 — Public Network Surface.
// CC2-INT-B08 — Public Referral Initiation (POST /api/public/referrals).
// These endpoints are intentionally anonymous — no JWT or platform_session required.
// Tenant isolation is enforced via the X-Tenant-Id header, which is resolved
// server-side by the Next.js BFF from the request subdomain → Tenant service lookup.
// The caller (Next.js Server Component / BFF proxy) NEVER reads this header from user input;
// it resolves the tenant from the subdomain and forwards only the GUID.
//
// BLK-SEC-02-02: Trust boundary enforced via two-layer validation:
//   Layer 1 — X-Internal-Gateway-Secret: proves request passed through the trusted YARP gateway.
//   Layer 2 — X-Tenant-Id-Sig: HMAC-SHA256 of X-Tenant-Id signed by the BFF using
//             PublicTrustBoundary:InternalRequestSecret; proves X-Tenant-Id was not spoofed.
//
// Spoofed X-Tenant-Id from direct gateway callers → rejected at Layer 2 (no valid HMAC).
// Direct-to-service requests bypassing the gateway → rejected at Layer 1 (no gateway secret).
public static class PublicNetworkEndpoints
{
    public static void MapPublicNetworkEndpoints(this WebApplication app)
    {
        // All public routes share the /api/public/network prefix.
        // The Gateway is configured to route /careconnect/api/public/** anonymously.
        var group = app.MapGroup("/api/public/network");

        // ── GET /api/public/network ─────────────────────────────────────────
        // Lists all networks for the resolved tenant.
        // Header: X-Tenant-Id (GUID, resolved from subdomain by Next.js BFF)
        group.MapGet("/", async (
            HttpContext    http,
            IConfiguration config,
            INetworkRepository repo,
            CancellationToken  ct) =>
        {
            var tenantId = ValidateTrustBoundaryAndResolveTenantId(http, config);
            if (tenantId == null)
                return Results.Problem(statusCode: StatusCodes.Status403Forbidden,
                    detail: "Request origin could not be verified.");

            var networks = await repo.GetAllByTenantAsync(tenantId.Value, ct);

            var summaries = new List<PublicNetworkSummary>(networks.Count);
            foreach (var n in networks)
            {
                var detail = await repo.GetWithProvidersAsync(tenantId.Value, n.Id, ct);
                summaries.Add(new PublicNetworkSummary(
                    n.Id,
                    n.Name,
                    n.Description,
                    detail?.NetworkProviders.Count ?? 0));
            }

            return Results.Ok(summaries);
        }).AllowAnonymous();

        // ── GET /api/public/network/{id}/providers ──────────────────────────
        group.MapGet("/{id:guid}/providers", async (
            Guid           id,
            HttpContext    http,
            IConfiguration config,
            INetworkRepository repo,
            CancellationToken  ct) =>
        {
            var tenantId = ValidateTrustBoundaryAndResolveTenantId(http, config);
            if (tenantId == null)
                return Results.Problem(statusCode: StatusCodes.Status403Forbidden,
                    detail: "Request origin could not be verified.");

            var network = await repo.GetByIdAsync(tenantId.Value, id, ct);
            if (network == null)
                return Results.NotFound();

            var providers = await repo.GetNetworkProvidersAsync(tenantId.Value, id, ct);

            var items = providers
                .Select(p => new PublicProviderItem(
                    p.Id,
                    p.Name,
                    p.OrganizationName,
                    p.Phone,
                    p.City,
                    p.State,
                    p.PostalCode,
                    p.IsActive,
                    p.AcceptingReferrals,
                    p.AccessStage,
                    null))
                .ToList();

            return Results.Ok(items);
        }).AllowAnonymous();

        // ── GET /api/public/network/{id}/providers/markers ──────────────────
        group.MapGet("/{id:guid}/providers/markers", async (
            Guid           id,
            HttpContext    http,
            IConfiguration config,
            INetworkRepository repo,
            CancellationToken  ct) =>
        {
            var tenantId = ValidateTrustBoundaryAndResolveTenantId(http, config);
            if (tenantId == null)
                return Results.Problem(statusCode: StatusCodes.Status403Forbidden,
                    detail: "Request origin could not be verified.");

            var network = await repo.GetByIdAsync(tenantId.Value, id, ct);
            if (network == null)
                return Results.NotFound();

            var providers = await repo.GetNetworkProvidersAsync(tenantId.Value, id, ct);

            var markers = providers
                .Where(p => p.Latitude.HasValue && p.Longitude.HasValue)
                .Select(p => new PublicProviderMarker(
                    p.Id,
                    p.Name,
                    p.OrganizationName,
                    p.City,
                    p.State,
                    p.AcceptingReferrals,
                    p.Latitude!.Value,
                    p.Longitude!.Value))
                .ToList();

            return Results.Ok(markers);
        }).AllowAnonymous();

        // ── GET /api/public/network/{id}/detail ────────────────────────────
        group.MapGet("/{id:guid}/detail", async (
            Guid           id,
            HttpContext    http,
            IConfiguration config,
            INetworkRepository repo,
            CancellationToken  ct) =>
        {
            var tenantId = ValidateTrustBoundaryAndResolveTenantId(http, config);
            if (tenantId == null)
                return Results.Problem(statusCode: StatusCodes.Status403Forbidden,
                    detail: "Request origin could not be verified.");

            var network = await repo.GetWithProvidersAsync(tenantId.Value, id, ct);
            if (network == null)
                return Results.NotFound();

            var providers = await repo.GetNetworkProvidersAsync(tenantId.Value, id, ct);

            var items = providers
                .Select(p => new PublicProviderItem(
                    p.Id,
                    p.Name,
                    p.OrganizationName,
                    p.Phone,
                    p.City,
                    p.State,
                    p.PostalCode,
                    p.IsActive,
                    p.AcceptingReferrals,
                    p.AccessStage,
                    null))
                .ToList();

            var markers = providers
                .Where(p => p.Latitude.HasValue && p.Longitude.HasValue)
                .Select(p => new PublicProviderMarker(
                    p.Id,
                    p.Name,
                    p.OrganizationName,
                    p.City,
                    p.State,
                    p.AcceptingReferrals,
                    p.Latitude!.Value,
                    p.Longitude!.Value))
                .ToList();

            var detail = new PublicNetworkDetail(
                network.Id,
                network.Name,
                network.Description,
                items,
                markers);

            return Results.Ok(detail);
        }).AllowAnonymous();

        // ── POST /api/public/referrals ──────────────────────────────────────
        // CC2-INT-B08 — Public referral initiation.
        // Accepts an unauthenticated referral submission from the public network directory.
        // Rate-limited (10 req/min per IP, policy registered in Program.cs) to prevent abuse.
        // Tenant isolation: X-Tenant-Id set server-side by Next.js BFF from subdomain — never
        // read from user input.
        // Token/notification flow: delegated to IReferralService.CreateAsync, which fires:
        //   - SendNewReferralNotificationAsync  (email + signed token for URL-stage providers)
        //   - SendProviderAssignedNotificationAsync (platform Notifications → portal visibility)
        app.MapPost("/api/public/referrals", async (
            PublicReferralRequest req,
            HttpContext           http,
            IConfiguration       config,
            IProviderRepository  providerRepo,
            IReferralService     referralSvc,
            ILoggerFactory       loggerFactory,
            CancellationToken    ct) =>
        {
            var logger = loggerFactory.CreateLogger("CareConnect.PublicReferrals");
            return await HandlePublicReferral(req, http, config, providerRepo, referralSvc, logger, ct);
        })
        .AllowAnonymous()
        .RequireRateLimiting("public-referral-limit");
    }

    private static async Task<IResult> HandlePublicReferral(
        PublicReferralRequest req,
        HttpContext           http,
        IConfiguration       config,
        IProviderRepository  providerRepo,
        IReferralService     referralSvc,
        ILogger              logger,
        CancellationToken    ct)
    {
            var tenantId = ValidateTrustBoundaryAndResolveTenantId(http, config);
            if (tenantId == null)
                return Results.Problem(statusCode: StatusCodes.Status403Forbidden,
                    detail: "Request origin could not be verified.");

            // Input validation
            var errors = ValidatePublicReferralRequest(req);
            if (errors.Count > 0)
                return Results.UnprocessableEntity(new { message = "Validation failed.", errors });

            // Provider lookup + AcceptingReferrals guard.
            // Cross-tenant lookup because providers are a platform-wide marketplace.
            Provider? provider;
            try { provider = await providerRepo.GetByIdCrossAsync(req.ProviderId, ct); }
            catch { provider = null; }

            if (provider is null)
                return Results.NotFound(new { message = "Provider not found." });

            if (!provider.AcceptingReferrals)
                return Results.UnprocessableEntity(new
                {
                    message = "This provider is not currently accepting referrals."
                });

            // Map to the internal CreateReferralRequest.
            // ReferrerName/ReferrerEmail drive the signed-token email notification flow.
            var createReq = new CreateReferralRequest
            {
                ProviderId              = req.ProviderId,
                ClientFirstName         = req.PatientFirstName.Trim(),
                ClientLastName          = req.PatientLastName.Trim(),
                ClientPhone             = req.PatientPhone.Trim(),
                ClientEmail             = req.PatientEmail?.Trim() ?? string.Empty,
                RequestedService        = string.IsNullOrWhiteSpace(req.ServiceType)
                                            ? "General Referral"
                                            : req.ServiceType.Trim(),
                Urgency                 = Referral.ValidUrgencies.Normal,
                Notes                   = req.Notes?.Trim(),
                ReferrerName            = req.SenderName.Trim(),
                ReferrerEmail           = req.SenderEmail.Trim(),
                ReferringOrganizationId = null,   // public — no org context
                ReceivingOrganizationId = null,
            };

            // Create referral via the existing pipeline.
            // CreateAsync persists the referral and fires fire-and-observe notifications.
            // userId = null (anonymous submission).
            try
            {
                var referral = await referralSvc.CreateAsync(tenantId.Value, userId: null, createReq, ct);

                logger.LogInformation(
                    "Public referral submitted: ReferralId={ReferralId} ProviderId={ProviderId} " +
                    "Stage={Stage} Tenant={TenantId}",
                    referral.Id, req.ProviderId, provider.AccessStage, tenantId.Value);

                var response = new PublicReferralResponse(
                    referral.Id,
                    provider.Id,
                    provider.Name,
                    provider.AccessStage,
                    "Referral submitted successfully. The provider will be in touch shortly.");

                return Results.Created($"/api/public/referrals/{referral.Id}", response);
            }
            catch (NotFoundException ex)
            {
                logger.LogWarning(ex, "Public referral: provider not found mid-creation.");
                return Results.NotFound(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Public referral creation failed for provider {ProviderId}.", req.ProviderId);
                return Results.Problem("An unexpected error occurred while submitting your referral.");
            }
    }

    // ── Trust Boundary Validation (BLK-SEC-02-02) ─────────────────────────

    /// <summary>
    /// Validates the two-layer public trust boundary and returns the resolved TenantId.
    ///
    /// Layer 1 — X-Internal-Gateway-Secret:
    ///   Proves the request was forwarded by the trusted YARP gateway.
    ///   The gateway strips any client-supplied value and injects its own configured secret.
    ///   A direct caller bypassing the gateway cannot supply the correct value.
    ///
    /// Layer 2 — X-Tenant-Id-Sig (HMAC-SHA256):
    ///   Proves X-Tenant-Id was signed by the trusted Next.js BFF.
    ///   HMAC-SHA256(X-Tenant-Id, InternalRequestSecret) computed server-side by the BFF.
    ///   A caller going through the gateway but supplying an arbitrary X-Tenant-Id
    ///   cannot forge the signature without knowing the shared secret.
    ///
    /// Returns null and logs a warning if validation fails.
    /// The fallback path (validation disabled / secret not configured) is intentionally
    /// limited to environments where the secret is not set — logged as a warning.
    /// </summary>
    private static Guid? ValidateTrustBoundaryAndResolveTenantId(
        HttpContext    http,
        IConfiguration config)
    {
        var logger = http.RequestServices
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger("CareConnect.PublicTrustBoundary");

        var secret = config["PublicTrustBoundary:InternalRequestSecret"];

        if (string.IsNullOrWhiteSpace(secret))
        {
            logger.LogWarning(
                "PublicTrustBoundary:InternalRequestSecret is not configured — " +
                "trust boundary validation is DISABLED. Set this value in all non-dev environments. " +
                "Path={Path}", http.Request.Path);
            return ResolveTenantIdRaw(http);
        }

        // Layer 1: validate gateway origin marker
        var gatewaySecret = http.Request.Headers["X-Internal-Gateway-Secret"].FirstOrDefault();
        if (gatewaySecret != secret)
        {
            logger.LogWarning(
                "Public request rejected: X-Internal-Gateway-Secret mismatch (Layer 1). " +
                "RemoteIp={RemoteIp} Path={Path}",
                http.Connection.RemoteIpAddress, http.Request.Path);
            return null;
        }

        // Layer 2: validate HMAC signature of X-Tenant-Id
        var tenantIdRaw = http.Request.Headers["X-Tenant-Id"].FirstOrDefault();
        var sig         = http.Request.Headers["X-Tenant-Id-Sig"].FirstOrDefault();

        if (string.IsNullOrWhiteSpace(tenantIdRaw))
        {
            logger.LogWarning(
                "Public request rejected: X-Tenant-Id header missing (Layer 2). " +
                "RemoteIp={RemoteIp} Path={Path}",
                http.Connection.RemoteIpAddress, http.Request.Path);
            return null;
        }

        if (string.IsNullOrWhiteSpace(sig))
        {
            logger.LogWarning(
                "Public request rejected: X-Tenant-Id-Sig header missing (Layer 2). " +
                "RemoteIp={RemoteIp} Path={Path}",
                http.Connection.RemoteIpAddress, http.Request.Path);
            return null;
        }

        if (!TryValidateHmac(tenantIdRaw, sig, secret))
        {
            logger.LogWarning(
                "Public request rejected: X-Tenant-Id-Sig HMAC validation failed (Layer 2). " +
                "RemoteIp={RemoteIp} Path={Path}",
                http.Connection.RemoteIpAddress, http.Request.Path);
            return null;
        }

        if (!Guid.TryParse(tenantIdRaw, out var tenantId))
        {
            logger.LogWarning(
                "Public request rejected: X-Tenant-Id is not a valid GUID. " +
                "RemoteIp={RemoteIp} Path={Path}",
                http.Connection.RemoteIpAddress, http.Request.Path);
            return null;
        }

        return tenantId;
    }

    /// <summary>
    /// Validates HMAC-SHA256(data, secret) against the provided base64-encoded signature.
    /// Uses constant-time comparison to prevent timing side-channel attacks.
    /// </summary>
    private static bool TryValidateHmac(string data, string sig, string secret)
    {
        try
        {
            byte[] sigBytes;
            try { sigBytes = Convert.FromBase64String(sig); }
            catch { return false; }

            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
            var expected = hmac.ComputeHash(Encoding.UTF8.GetBytes(data));

            if (sigBytes.Length != expected.Length) return false;
            return CryptographicOperations.FixedTimeEquals(expected, sigBytes);
        }
        catch
        {
            return false;
        }
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Raw tenant ID extraction — used only when trust boundary validation is disabled
    /// (unconfigured secret, typically in local dev without the full gateway stack).
    /// </summary>
    private static Guid? ResolveTenantIdRaw(HttpContext http)
    {
        var raw = http.Request.Headers["X-Tenant-Id"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(raw)) return null;
        return Guid.TryParse(raw, out var id) ? id : null;
    }

    /// <summary>
    /// CC2-INT-B08: Validates a public referral submission.
    /// Returns a dictionary of field → error message for any validation failures.
    /// </summary>
    private static Dictionary<string, string> ValidatePublicReferralRequest(PublicReferralRequest req)
    {
        var errors = new Dictionary<string, string>();

        if (req.ProviderId == Guid.Empty)
            errors["providerId"] = "A valid provider ID is required.";

        if (string.IsNullOrWhiteSpace(req.SenderName) || req.SenderName.Trim().Length < 2)
            errors["senderName"] = "Your name is required (minimum 2 characters).";
        else if (req.SenderName.Length > 200)
            errors["senderName"] = "Name must not exceed 200 characters.";

        if (string.IsNullOrWhiteSpace(req.SenderEmail))
            errors["senderEmail"] = "Your email address is required.";
        else if (!IsValidEmail(req.SenderEmail))
            errors["senderEmail"] = "A valid email address is required.";

        if (string.IsNullOrWhiteSpace(req.PatientFirstName) || req.PatientFirstName.Trim().Length < 1)
            errors["patientFirstName"] = "Patient first name is required.";
        else if (req.PatientFirstName.Length > 100)
            errors["patientFirstName"] = "First name must not exceed 100 characters.";

        if (string.IsNullOrWhiteSpace(req.PatientLastName) || req.PatientLastName.Trim().Length < 1)
            errors["patientLastName"] = "Patient last name is required.";
        else if (req.PatientLastName.Length > 100)
            errors["patientLastName"] = "Last name must not exceed 100 characters.";

        if (string.IsNullOrWhiteSpace(req.PatientPhone))
            errors["patientPhone"] = "Patient phone number is required.";
        else if (req.PatientPhone.Trim().Length < 7 || req.PatientPhone.Length > 30)
            errors["patientPhone"] = "Please enter a valid phone number.";

        if (!string.IsNullOrWhiteSpace(req.PatientEmail) && !IsValidEmail(req.PatientEmail))
            errors["patientEmail"] = "Please enter a valid patient email address.";

        if (req.ServiceType is not null && req.ServiceType.Length > 200)
            errors["serviceType"] = "Service type must not exceed 200 characters.";

        if (req.Notes is not null && req.Notes.Length > 2000)
            errors["notes"] = "Notes must not exceed 2000 characters.";

        return errors;
    }

    private static bool IsValidEmail(string email)
    {
        try { _ = new MailAddress(email.Trim()); return true; }
        catch { return false; }
    }
}
