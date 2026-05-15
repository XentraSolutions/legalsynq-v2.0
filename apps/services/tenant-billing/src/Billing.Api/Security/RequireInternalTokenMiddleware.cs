using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Billing.Api.Security;

/// <summary>
/// Internal-token gate for the Billing API. Billing is a private microservice
/// — only the Monk BFF (or another internal caller acting in its place during
/// development/testing) is allowed to reach <c>/api/*</c>. The shared secret
/// is supplied out-of-band:
///
///   1. Environment variable <c>BILLING_INTERNAL_TOKEN</c> wins.
///   2. Configuration key <c>Billing:InternalToken</c> as a fallback for local
///      development. Production deployments MUST use the env var.
///
/// Behaviour:
/// <list type="bullet">
///   <item>Health endpoints (<c>/health</c>, <c>/healthz</c>) bypass this
///         middleware entirely so liveness probes work without the token.</item>
///   <item>Anything outside <c>/api/</c> also bypasses (Swagger UI, static
///         assets, the root probe).</item>
///   <item>For <c>/api/*</c> requests:
///     <list type="number">
///       <item>If no token is configured at all, the service fails closed
///             (401) and logs an error. This guarantees that an unconfigured
///             Billing instance cannot be quietly reachable.</item>
///       <item>If the inbound <c>X-Internal-Token</c> header is missing, empty,
///             or does not match the configured token, return 401.</item>
///     </list>
///   </item>
///   <item>Token comparison is constant-time. The token value is never logged.</item>
///   <item>The token is only accepted from the <c>X-Internal-Token</c> request
///         header — never from query string or cookie.</item>
/// </list>
///
/// This middleware MUST run before <see cref="Tenancy.TenantResolutionMiddleware"/>
/// so that tenant-header validation is only reached by trusted internal callers.
/// </summary>
public sealed class RequireInternalTokenMiddleware
{
    public const string HeaderName = "X-Internal-Token";
    public const string ProtectedPathPrefix = "/api/";
    public const string ConfigurationKey = "Billing:InternalToken";
    public const string EnvironmentVariableName = "BILLING_INTERNAL_TOKEN";

    private static readonly string[] BypassPaths = { "/health", "/healthz" };

    private readonly RequestDelegate _next;
    private readonly string? _expectedToken;
    private readonly ILogger<RequireInternalTokenMiddleware> _logger;

    public RequireInternalTokenMiddleware(
        RequestDelegate next,
        IConfiguration configuration,
        ILogger<RequireInternalTokenMiddleware> logger)
    {
        _next = next;
        _logger = logger;
        _expectedToken = Environment.GetEnvironmentVariable(EnvironmentVariableName)
                         ?? configuration[ConfigurationKey];
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value ?? string.Empty;

        // 1. Health endpoints always bypass.
        foreach (var bypass in BypassPaths)
        {
            if (path.Equals(bypass, StringComparison.OrdinalIgnoreCase)
                || path.StartsWith(bypass + "/", StringComparison.OrdinalIgnoreCase))
            {
                await _next(context);
                return;
            }
        }

        // 2. Anything outside /api/ bypasses (Swagger, static, etc.).
        if (!path.StartsWith(ProtectedPathPrefix, StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        // 3. Fail closed when no token is configured.
        if (string.IsNullOrEmpty(_expectedToken))
        {
            _logger.LogError(
                "Billing internal token is not configured. Set {EnvVar} or {ConfigKey}. " +
                "Rejecting {Method} {Path} with 401.",
                EnvironmentVariableName,
                ConfigurationKey,
                context.Request.Method,
                path);

            await WriteUnauthorizedAsync(
                context,
                "Billing internal token is not configured. The service cannot accept API traffic.");
            return;
        }

        // 4. Header must be present and non-empty.
        if (!context.Request.Headers.TryGetValue(HeaderName, out var values)
            || values.Count == 0
            || string.IsNullOrEmpty(values[0]))
        {
            await WriteUnauthorizedAsync(context, $"Missing required '{HeaderName}' header.");
            return;
        }

        // 5. Constant-time comparison. Never log the inbound token.
        if (!FixedTimeEquals(values[0]!, _expectedToken))
        {
            await WriteUnauthorizedAsync(context, $"Invalid '{HeaderName}' header.");
            return;
        }

        await _next(context);
    }

    /// <summary>
    /// Constant-time string comparison. Returns false immediately if the
    /// lengths differ — the length of an internal-service shared secret is
    /// not considered sensitive in this context.
    /// </summary>
    private static bool FixedTimeEquals(string a, string b)
    {
        if (a.Length != b.Length) return false;
        var diff = 0;
        for (var i = 0; i < a.Length; i++)
        {
            diff |= a[i] ^ b[i];
        }
        return diff == 0;
    }

    private static async Task WriteUnauthorizedAsync(HttpContext context, string detail)
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        context.Response.ContentType = "application/problem+json";
        await context.Response.WriteAsJsonAsync(new
        {
            type = "https://datatracker.ietf.org/doc/html/rfc7235#section-3.1",
            title = "Unauthorized",
            status = StatusCodes.Status401Unauthorized,
            detail
        });
    }
}
