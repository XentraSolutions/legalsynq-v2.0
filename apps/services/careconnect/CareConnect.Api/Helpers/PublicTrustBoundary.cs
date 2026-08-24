using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LegalSynq.AuditClient;
using LegalSynq.AuditClient.DTOs;
using LegalSynq.AuditClient.Enums;
using AuditVisibility = LegalSynq.AuditClient.Enums.VisibilityScope;

namespace CareConnect.Api.Helpers;

/// <summary>
/// Shared trust-boundary validation for every anonymous ("public") CareConnect endpoint —
/// originally implemented once inline in PublicNetworkEndpoints, extracted here so a new
/// anonymous surface (e.g. PublicRepresentativeEndpoints) can reuse the exact same,
/// security-critical check rather than re-implementing it.
///
/// BLK-SEC-02-02: Trust boundary enforced via two-layer validation:
///   Layer 1 — X-Internal-Gateway-Secret: proves request passed through the trusted YARP gateway.
///   Layer 2 — X-Tenant-Id-Sig: HMAC-SHA256 of X-Tenant-Id signed by the BFF using
///             PublicTrustBoundary:InternalRequestSecret; proves X-Tenant-Id was not spoofed.
///
/// Spoofed X-Tenant-Id from direct gateway callers → rejected at Layer 2 (no valid HMAC).
/// Direct-to-service requests bypassing the gateway → rejected at Layer 1 (no gateway secret).
/// </summary>
public static class PublicTrustBoundary
{
    /// <summary>
    /// Validates the two-layer public trust boundary and returns the resolved TenantId.
    /// Returns null and logs a warning if validation fails. The fallback path (validation
    /// disabled / secret not configured) is intentionally limited to environments where the
    /// secret is not set — logged as a warning.
    /// </summary>
    public static Guid? ValidateAndResolveTenantId(
        HttpContext    http,
        IConfiguration config,
        string         sourceService)
    {
        var logger = http.RequestServices
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger("CareConnect.PublicTrustBoundary");

        var secret = config["PublicTrustBoundary:InternalRequestSecret"];

        // BLK-OBS-01: resolve correlation/request ID for all security-event log entries.
        var requestId = http.Items["CorrelationId"]?.ToString() ?? http.TraceIdentifier;

        if (string.IsNullOrWhiteSpace(secret))
        {
            logger.LogWarning(
                "PublicTrustBoundary:InternalRequestSecret is not configured — " +
                "trust boundary validation is DISABLED. Set this value in all non-dev environments. " +
                "Path={Path} RequestId={RequestId}", http.Request.Path, requestId);
            return ResolveTenantIdRaw(http);
        }

        // Layer 1: validate gateway origin marker
        var gatewaySecret = http.Request.Headers["X-Internal-Gateway-Secret"].FirstOrDefault();
        if (gatewaySecret != secret)
        {
            logger.LogWarning(
                "Public request rejected: X-Internal-Gateway-Secret mismatch (Layer 1). " +
                "RemoteIp={RemoteIp} Path={Path} RequestId={RequestId}",
                http.Connection.RemoteIpAddress, http.Request.Path, requestId);
            EmitTrustBoundaryRejectedAudit(http, sourceService, "layer1-gateway-secret-mismatch", requestId);
            return null;
        }

        // Layer 2: validate HMAC signature of X-Tenant-Id
        var tenantIdRaw = http.Request.Headers["X-Tenant-Id"].FirstOrDefault();
        var sig         = http.Request.Headers["X-Tenant-Id-Sig"].FirstOrDefault();

        if (string.IsNullOrWhiteSpace(tenantIdRaw))
        {
            logger.LogWarning(
                "Public request rejected: X-Tenant-Id header missing (Layer 2). " +
                "RemoteIp={RemoteIp} Path={Path} RequestId={RequestId}",
                http.Connection.RemoteIpAddress, http.Request.Path, requestId);
            EmitTrustBoundaryRejectedAudit(http, sourceService, "layer2-tenant-id-missing", requestId);
            return null;
        }

        if (string.IsNullOrWhiteSpace(sig))
        {
            logger.LogWarning(
                "Public request rejected: X-Tenant-Id-Sig header missing (Layer 2). " +
                "RemoteIp={RemoteIp} Path={Path} RequestId={RequestId}",
                http.Connection.RemoteIpAddress, http.Request.Path, requestId);
            EmitTrustBoundaryRejectedAudit(http, sourceService, "layer2-tenant-id-sig-missing", requestId);
            return null;
        }

        if (!TryValidateHmac(tenantIdRaw, sig, secret))
        {
            logger.LogWarning(
                "Public request rejected: X-Tenant-Id-Sig HMAC validation failed (Layer 2). " +
                "RemoteIp={RemoteIp} Path={Path} RequestId={RequestId}",
                http.Connection.RemoteIpAddress, http.Request.Path, requestId);
            EmitTrustBoundaryRejectedAudit(http, sourceService, "layer2-hmac-validation-failed", requestId);
            return null;
        }

        if (!Guid.TryParse(tenantIdRaw, out var tenantId))
        {
            logger.LogWarning(
                "Public request rejected: X-Tenant-Id is not a valid GUID. " +
                "RemoteIp={RemoteIp} Path={Path} RequestId={RequestId}",
                http.Connection.RemoteIpAddress, http.Request.Path, requestId);
            EmitTrustBoundaryRejectedAudit(http, sourceService, "layer2-tenant-id-invalid-guid", requestId);
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

    // ── BLK-COMP-01: Trust boundary rejection audit ───────────────────────────
    // Emits security.trust_boundary.rejected to the Audit Service for every failed
    // validation so that direct-service probes and header spoofing attempts are
    // permanently reconstructable under SOC 2 / HIPAA audit.
    //
    // Fire-and-observe: caller does NOT await — this never gates the 403 response
    // on audit delivery success ("persist-first, audit-second" rule).
    private static void EmitTrustBoundaryRejectedAudit(
        HttpContext http,
        string      sourceService,
        string      reason,
        string      requestId)
    {
        var auditClient = http.RequestServices.GetService<IAuditEventClient>();
        if (auditClient is null) return;

        _ = auditClient.IngestAsync(new IngestAuditEventRequest
        {
            EventType     = "security.trust_boundary.rejected",
            EventCategory = EventCategory.Security,
            SourceSystem  = "care-connect",
            SourceService = sourceService,
            Visibility    = AuditVisibility.Platform,
            Severity      = SeverityLevel.Warn,
            OccurredAtUtc = DateTimeOffset.UtcNow,
            Action        = "TrustBoundaryRejected",
            Description   = $"Public request rejected at trust boundary: {reason}.",
            Outcome       = "denied",
            Actor = new AuditEventActorDto
            {
                Type      = ActorType.Anonymous,
                IpAddress = http.Connection.RemoteIpAddress?.ToString(),
            },
            Scope         = new AuditEventScopeDto { ScopeType = ScopeType.Service },
            CorrelationId = requestId,
            Metadata      = JsonSerializer.Serialize(new
            {
                reason = reason,
                path   = http.Request.Path.Value,
            }),
            Tags = ["security", "trust-boundary", "rejection"],
        });
    }
}
