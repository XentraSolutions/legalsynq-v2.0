using System.Net.Http.Json;
using Flow.Application.Adapters.AuditAdapter;
using Microsoft.Extensions.Logging;

namespace Flow.Infrastructure.Adapters;

/// <summary>
/// Optional HTTP-backed audit adapter. Activated only when
/// <c>Audit:BaseUrl</c> is configured. Decorates a fallback adapter so
/// that transient failures degrade gracefully without breaking the
/// originating request.
/// </summary>
public sealed class HttpAuditAdapter : IAuditAdapter
{
    private readonly HttpClient _http;
    private readonly IAuditAdapter _fallback;
    private readonly ILogger<HttpAuditAdapter> _log;

    public HttpAuditAdapter(HttpClient http, IAuditAdapter fallback, ILogger<HttpAuditAdapter> log)
    {
        _http = http;
        _fallback = fallback;
        _log = log;
    }

    public async Task WriteEventAsync(AuditEvent auditEvent, CancellationToken cancellationToken = default)
    {
        try
        {
            using var resp = await _http.PostAsJsonAsync("audit/events", auditEvent, cancellationToken);
            if (!resp.IsSuccessStatusCode)
            {
                _log.LogWarning(
                    "Audit POST returned {StatusCode}; falling back to logging adapter.",
                    (int)resp.StatusCode);
                await _fallback.WriteEventAsync(auditEvent, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Audit POST failed; falling back to logging adapter.");
            await _fallback.WriteEventAsync(auditEvent, cancellationToken);
        }
    }
}
