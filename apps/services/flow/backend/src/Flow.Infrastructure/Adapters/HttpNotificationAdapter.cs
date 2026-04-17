using System.Net.Http.Json;
using Flow.Application.Adapters.NotificationAdapter;
using Microsoft.Extensions.Logging;

namespace Flow.Infrastructure.Adapters;

/// <summary>
/// Optional HTTP-backed notification adapter. Activated only when
/// <c>Notifications:BaseUrl</c> is configured.
/// </summary>
public sealed class HttpNotificationAdapter : INotificationAdapter
{
    private readonly HttpClient _http;
    private readonly INotificationAdapter _fallback;
    private readonly ILogger<HttpNotificationAdapter> _log;

    public HttpNotificationAdapter(HttpClient http, INotificationAdapter fallback, ILogger<HttpNotificationAdapter> log)
    {
        _http = http;
        _fallback = fallback;
        _log = log;
    }

    public async Task SendAsync(NotificationMessage message, CancellationToken cancellationToken = default)
    {
        try
        {
            using var resp = await _http.PostAsJsonAsync("notifications", message, cancellationToken);
            if (!resp.IsSuccessStatusCode)
            {
                _log.LogWarning(
                    "Notifications POST returned {StatusCode}; falling back to logging adapter.",
                    (int)resp.StatusCode);
                await _fallback.SendAsync(message, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Notifications POST failed; falling back to logging adapter.");
            await _fallback.SendAsync(message, cancellationToken);
        }
    }
}
