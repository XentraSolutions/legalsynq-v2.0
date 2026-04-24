using System.Net.Http.Json;
using Microsoft.Extensions.Options;

namespace Support.Api.Notifications;

/// <summary>
/// HTTP adapter that forwards notification requests to the Notifications
/// Service. Failures are logged but never propagated — Support Service
/// writes must not be rolled back due to notification transport issues.
/// </summary>
public sealed class HttpNotificationPublisher : INotificationPublisher
{
    public const string HttpClientName = "support-notifications";

    private readonly HttpClient _http;
    private readonly IOptionsMonitor<NotificationOptions> _options;
    private readonly ILogger<HttpNotificationPublisher> _log;

    public HttpNotificationPublisher(
        HttpClient http,
        IOptionsMonitor<NotificationOptions> options,
        ILogger<HttpNotificationPublisher> log)
    {
        _http = http;
        _options = options;
        _log = log;
    }

    public async Task PublishAsync(SupportNotification notification, CancellationToken ct = default)
    {
        var opts = _options.CurrentValue;
        if (!opts.Enabled)
        {
            _log.LogDebug(
                "Notifications disabled; suppressing HTTP dispatch event={EventType} ticket={TicketNumber}",
                notification.EventType, notification.TicketNumber);
            return;
        }

        if (string.IsNullOrWhiteSpace(opts.BaseUrl))
        {
            _log.LogWarning(
                "Notifications enabled in Http mode but BaseUrl is unset; skipping event={EventType} ticket={TicketNumber}",
                notification.EventType, notification.TicketNumber);
            return;
        }

        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(Math.Max(1, opts.TimeoutSeconds)));

            var url = CombineUrl(opts.BaseUrl!, "notifications");
            using var resp = await _http.PostAsJsonAsync(url, notification, cts.Token);
            if (!resp.IsSuccessStatusCode)
            {
                _log.LogWarning(
                    "Notifications dispatch returned {Status} event={EventType} ticket={TicketNumber}",
                    (int)resp.StatusCode, notification.EventType, notification.TicketNumber);
            }
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex,
                "Notifications dispatch failed event={EventType} ticket={TicketNumber}",
                notification.EventType, notification.TicketNumber);
        }
    }

    private static string CombineUrl(string baseUrl, string path)
    {
        var b = baseUrl.TrimEnd('/');
        var p = path.TrimStart('/');
        return $"{b}/{p}";
    }
}
