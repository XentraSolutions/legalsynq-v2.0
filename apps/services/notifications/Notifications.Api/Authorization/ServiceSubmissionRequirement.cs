using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Notifications.Api.Authorization;

/// <summary>
/// LS-NOTIF-CORE-021 — authorization requirement for the
/// <c>POST /v1/notifications</c> producer endpoint.
///
/// <para>
/// The handler accepts the request when:
/// <list type="bullet">
///   <item>Any authenticated caller (user or service JWT) is present.</item>
///   <item>An unauthenticated legacy caller supplies a valid
///         <c>X-Tenant-Id</c> header — accepted with a structured
///         <c>[LEGACY SUBMISSION]</c> WARNING so migration progress can
///         be tracked in log dashboards.</item>
/// </list>
/// </para>
///
/// <para>
/// Requests with neither a valid JWT nor a valid <c>X-Tenant-Id</c> header
/// are not succeeded, causing the framework to return <c>401 Unauthorized</c>.
/// </para>
/// </summary>
public sealed class ServiceSubmissionRequirement : IAuthorizationRequirement { }

/// <summary>
/// Handles <see cref="ServiceSubmissionRequirement"/>.
/// Registered as a singleton in <c>Program.cs</c>.
/// </summary>
public sealed class ServiceSubmissionHandler
    : AuthorizationHandler<ServiceSubmissionRequirement>
{
    private readonly IHttpContextAccessor _http;
    private readonly ILogger<ServiceSubmissionHandler> _logger;

    public ServiceSubmissionHandler(
        IHttpContextAccessor http,
        ILogger<ServiceSubmissionHandler> logger)
    {
        _http   = http;
        _logger = logger;
    }

    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        ServiceSubmissionRequirement requirement)
    {
        // ── Authenticated path (user JWT or service JWT) ─────────────────
        if (context.User.Identity?.IsAuthenticated == true)
        {
            var sub         = context.User.FindFirst("sub")?.Value ?? "(unknown)";
            var serviceName = context.User.FindFirst("svc")?.Value;
            var tenantId    = context.User.FindFirst("tenant_id")?.Value ?? "(unknown)";

            if (!string.IsNullOrEmpty(serviceName))
            {
                _logger.LogDebug(
                    "Service submission authorised via JWT. " +
                    "ServiceName={ServiceName} Sub={Sub} TenantId={TenantId}",
                    serviceName, sub, tenantId);
            }

            context.Succeed(requirement);
            return Task.CompletedTask;
        }

        // ── Legacy path — unauthenticated caller with X-Tenant-Id header ──
        var httpCtx     = _http.HttpContext;
        var tenantHeader = httpCtx?.Request.Headers["X-Tenant-Id"].FirstOrDefault();

        if (!string.IsNullOrEmpty(tenantHeader) && Guid.TryParse(tenantHeader, out _))
        {
            _logger.LogWarning(
                "[LEGACY SUBMISSION] Unauthenticated POST /v1/notifications accepted " +
                "via X-Tenant-Id header. TenantId={TenantId} Path={Path} " +
                "RemoteIp={RemoteIp}. " +
                "Migrate this caller to service-token authentication (LS-NOTIF-CORE-021).",
                tenantHeader,
                httpCtx?.Request.Path.Value,
                httpCtx?.Connection.RemoteIpAddress?.ToString());

            context.Succeed(requirement);
            return Task.CompletedTask;
        }

        // ── Rejected — no JWT and no valid X-Tenant-Id header ────────────
        _logger.LogWarning(
            "POST /v1/notifications rejected: no valid JWT and no X-Tenant-Id header. " +
            "Path={Path} RemoteIp={RemoteIp}",
            httpCtx?.Request.Path.Value,
            httpCtx?.Connection.RemoteIpAddress?.ToString());

        return Task.CompletedTask;
    }
}
