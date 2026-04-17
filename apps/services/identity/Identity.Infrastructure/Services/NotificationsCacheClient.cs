using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Identity.Infrastructure.Services;

/// <summary>
/// Configuration for <see cref="NotificationsCacheClient"/>. Bound from
/// the <c>NotificationsService</c> configuration section:
///
///   "NotificationsService": {
///     "BaseUrl":              "http://notifications-service:5005",
///     "TimeoutSeconds":       5,
///     "InternalServiceToken": "shared-secret-here"   // matches notifications' INTERNAL_SERVICE_TOKEN
///   }
///
/// When <see cref="BaseUrl"/> is empty/unset, identity skips the call —
/// notifications then falls back to its TTL-based cache freshness. The
/// <see cref="InternalServiceToken"/> must match the value notifications
/// expects in the <c>X-Internal-Service-Token</c> header (enforced by
/// <c>InternalTokenMiddleware</c> on every <c>/internal/*</c> route).
/// </summary>
public sealed class NotificationsServiceOptions
{
    public const string SectionName = "NotificationsService";

    public string? BaseUrl              { get; set; }
    public int     TimeoutSeconds       { get; set; } = 5;
    public string? InternalServiceToken { get; set; }
}

/// <summary>
/// Notifies the notifications service that role/membership state for a
/// tenant has changed so it can invalidate its in-process membership cache.
/// All calls are fire-and-observe: failures are logged but never gate the
/// caller (identity admin endpoints / role assignment services).
///
/// This is what lets the notifications service keep a long cache TTL for
/// cost reasons while still reflecting role-membership changes immediately
/// for high-stakes alerts (e.g. on-call notifications).
/// </summary>
public interface INotificationsCacheClient
{
    void InvalidateTenant(Guid tenantId, string eventType, string? reason = null);
}

public sealed class NotificationsCacheClient : INotificationsCacheClient
{
    // Matches the header enforced by Notifications.Api InternalTokenMiddleware
    // for every /internal/* route. Without it the call is rejected (401/503)
    // and notifications falls back to its TTL-based cache freshness.
    private const string TokenHeader = "X-Internal-Service-Token";

    private readonly IHttpClientFactory                 _httpClientFactory;
    private readonly NotificationsServiceOptions        _options;
    private readonly ILogger<NotificationsCacheClient>  _logger;

    public NotificationsCacheClient(
        IHttpClientFactory                 httpClientFactory,
        IOptions<NotificationsServiceOptions> options,
        ILogger<NotificationsCacheClient>  logger)
    {
        _httpClientFactory = httpClientFactory;
        _options           = options.Value;
        _logger            = logger;
    }

    public void InvalidateTenant(Guid tenantId, string eventType, string? reason = null)
    {
        if (tenantId == Guid.Empty) return;
        if (string.IsNullOrWhiteSpace(_options.BaseUrl))
        {
            _logger.LogDebug(
                "NotificationsService:BaseUrl not configured; skipping cache invalidation for tenant {TenantId}.",
                tenantId);
            return;
        }

        // Fire-and-observe: never block the originating admin call. Identity
        // emits the canonical audit event regardless of this side-effect.
        _ = Task.Run(async () =>
        {
            try
            {
                using var client = _httpClientFactory.CreateClient("NotificationsService");
                client.BaseAddress = new Uri(_options.BaseUrl!.TrimEnd('/') + "/");
                client.Timeout     = TimeSpan.FromSeconds(_options.TimeoutSeconds);

                if (!string.IsNullOrWhiteSpace(_options.InternalServiceToken))
                {
                    client.DefaultRequestHeaders.TryAddWithoutValidation(
                        TokenHeader, _options.InternalServiceToken);
                }

                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(_options.TimeoutSeconds));

                using var response = await client.PostAsJsonAsync(
                    "internal/membership-cache/invalidate",
                    new { tenantId, eventType, reason },
                    cts.Token);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning(
                        "Notifications membership-cache invalidate returned HTTP {Status} for tenant {TenantId} ({EventType}).",
                        (int)response.StatusCode, tenantId, eventType);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Notifications membership-cache invalidate failed for tenant {TenantId} ({EventType}). " +
                    "Notifications will rely on TTL-based freshness for this change.",
                    tenantId, eventType);
            }
        });
    }
}

/// <summary>
/// No-op fallback used when notifications integration is not configured.
/// Logs at debug level so test/dev environments stay quiet.
/// </summary>
public sealed class NoOpNotificationsCacheClient : INotificationsCacheClient
{
    public void InvalidateTenant(Guid tenantId, string eventType, string? reason = null) { }
}
