using System.Net.Http.Json;
using CareConnect.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace CareConnect.Infrastructure.Email;

/// <summary>
/// Sends transactional emails by calling the platform Notifications service internal API.
/// The Notifications service uses the platform-level SendGrid account configured in
/// Control Center — no per-tenant SMTP setup required.
///
/// Configuration key: NotificationsService:BaseUrl (default: http://localhost:5008)
/// </summary>
public class NotificationsServiceEmailSender : ISmtpEmailSender
{
    private readonly HttpClient _http;
    private readonly string     _baseUrl;
    private readonly ILogger<NotificationsServiceEmailSender> _logger;

    public NotificationsServiceEmailSender(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<NotificationsServiceEmailSender> logger)
    {
        _http    = httpClientFactory.CreateClient("NotificationsService");
        _baseUrl = (configuration["NotificationsService:BaseUrl"] ?? "http://localhost:5008").TrimEnd('/');
        _logger  = logger;
    }

    public async Task SendAsync(string toAddress, string subject, string htmlBody, CancellationToken ct = default)
    {
        var url     = $"{_baseUrl}/internal/send-email";
        var payload = new { to = toAddress, subject, htmlBody };

        _logger.LogInformation(
            "Dispatching email to {Recipient} via Notifications service ({Url})", toAddress, url);

        HttpResponseMessage response;
        try
        {
            response = await _http.PostAsJsonAsync(url, payload, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Notifications service unreachable while sending email to {Recipient}.", toAddress);
            throw new InvalidOperationException(
                "Email delivery failed — Notifications service is unreachable.", ex);
        }

        if (response.IsSuccessStatusCode)
        {
            _logger.LogInformation("Email dispatched successfully to {Recipient}", toAddress);
            return;
        }

        string body = string.Empty;
        try { body = await response.Content.ReadAsStringAsync(ct); } catch { }

        _logger.LogWarning(
            "Notifications service returned {StatusCode} while sending email to {Recipient}. Body: {Body}",
            (int)response.StatusCode, toAddress, body);

        throw new InvalidOperationException(
            $"Email delivery failed — Notifications service returned {(int)response.StatusCode}: {body}");
    }
}
