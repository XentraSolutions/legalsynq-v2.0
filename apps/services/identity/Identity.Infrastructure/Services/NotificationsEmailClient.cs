using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Identity.Infrastructure.Services;

/// <summary>
/// LS-ID-TNT-006: Sends transactional emails via the Notifications service
/// internal HTTP endpoint (<c>POST /internal/send-email</c>).
///
/// Mirrors the pattern of <see cref="NotificationsCacheClient"/> — uses the
/// same <c>IHttpClientFactory("NotificationsService")</c> named client and
/// the same <c>X-Internal-Service-Token</c> header for internal auth.
///
/// When <c>NotificationsService:BaseUrl</c> or <c>NotificationsService:PortalBaseUrl</c>
/// is not configured, the method returns <c>EmailConfigured = false</c> so the caller
/// can apply the appropriate fallback without treating it as a delivery failure.
/// </summary>
public interface INotificationsEmailClient
{
    /// <summary>
    /// Dispatches a password-reset email to <paramref name="toEmail"/>.
    /// </summary>
    /// <returns>
    /// A tuple where <c>EmailConfigured</c> indicates whether the integration
    /// is set up; <c>Success</c> indicates whether dispatch succeeded;
    /// <c>Error</c> contains a human-readable reason on failure.
    /// </returns>
    Task<(bool EmailConfigured, bool Success, string? Error)> SendPasswordResetEmailAsync(
        string            toEmail,
        string            displayName,
        string            resetLink,
        CancellationToken ct = default);
}

public sealed class NotificationsEmailClient : INotificationsEmailClient
{
    private const string TokenHeader = "X-Internal-Service-Token";

    private readonly IHttpClientFactory                  _httpClientFactory;
    private readonly NotificationsServiceOptions         _options;
    private readonly ILogger<NotificationsEmailClient>   _logger;

    public NotificationsEmailClient(
        IHttpClientFactory                    httpClientFactory,
        IOptions<NotificationsServiceOptions> options,
        ILogger<NotificationsEmailClient>     logger)
    {
        _httpClientFactory = httpClientFactory;
        _options           = options.Value;
        _logger            = logger;
    }

    public async Task<(bool EmailConfigured, bool Success, string? Error)> SendPasswordResetEmailAsync(
        string            toEmail,
        string            displayName,
        string            resetLink,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_options.BaseUrl) ||
            string.IsNullOrWhiteSpace(_options.PortalBaseUrl))
        {
            _logger.LogDebug(
                "NotificationsService:BaseUrl or PortalBaseUrl not configured; " +
                "password-reset email for {Email} will use the non-production token fallback.",
                toEmail);
            return (EmailConfigured: false, Success: false, Error: null);
        }

        try
        {
            using var client = _httpClientFactory.CreateClient("NotificationsService");
            client.BaseAddress = new Uri(_options.BaseUrl.TrimEnd('/') + "/");
            client.Timeout     = TimeSpan.FromSeconds(_options.TimeoutSeconds);

            if (!string.IsNullOrWhiteSpace(_options.InternalServiceToken))
            {
                client.DefaultRequestHeaders.TryAddWithoutValidation(
                    TokenHeader, _options.InternalServiceToken);
            }

            var payload = new
            {
                to      = toEmail,
                subject = "Reset your LegalSynq password",
                body    = BuildTextBody(displayName, resetLink),
                html    = BuildHtmlBody(displayName, resetLink),
            };

            using var response = await client.PostAsJsonAsync("internal/send-email", payload, ct);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation(
                    "[LS-ID-TNT-006] Password reset email dispatched to {Email}.",
                    toEmail);
                return (EmailConfigured: true, Success: true, Error: null);
            }

            var responseBody = await response.Content.ReadAsStringAsync(ct);
            _logger.LogWarning(
                "[LS-ID-TNT-006] Password reset email failed for {Email}: " +
                "notifications returned HTTP {Status}. Body: {Body}",
                toEmail, (int)response.StatusCode, responseBody);
            return (
                EmailConfigured: true,
                Success:         false,
                Error:           $"Notifications service returned HTTP {(int)response.StatusCode}.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "[LS-ID-TNT-006] Password reset email dispatch threw for {Email}.",
                toEmail);
            return (
                EmailConfigured: true,
                Success:         false,
                Error:           $"Email delivery error: {ex.GetType().Name}.");
        }
    }

    // ── Email templates ───────────────────────────────────────────────────────

    private static string BuildTextBody(string name, string link) =>
        $"""
        Hello {name},

        An administrator has requested a password reset for your LegalSynq account.

        Use the link below to set a new password. This link is valid for 24 hours.

          {link}

        If you did not expect this email, you can safely ignore it. Your password
        will not change unless you follow the link above.

        — The LegalSynq Team
        """;

    private static string BuildHtmlBody(string name, string link)
    {
        var safeName = HtmlEncode(name);
        var safeLink = HtmlEncode(link);

        return $"""
        <!DOCTYPE html>
        <html lang="en">
        <head>
          <meta charset="utf-8" />
          <meta name="viewport" content="width=device-width, initial-scale=1.0" />
          <title>Reset your LegalSynq password</title>
        </head>
        <body style="margin:0;padding:32px 0;background:#f9fafb;font-family:-apple-system,BlinkMacSystemFont,'Segoe UI',Roboto,Helvetica,Arial,sans-serif;">
          <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="max-width:520px;margin:0 auto;">
            <tr>
              <td style="background:#ffffff;border-radius:12px;padding:40px;border:1px solid #e5e7eb;">

                <!-- Wordmark -->
                <p style="margin:0 0 4px;font-size:22px;font-weight:700;color:#111827;letter-spacing:-0.3px;">LegalSynq</p>
                <hr style="border:none;border-top:1px solid #f3f4f6;margin:16px 0 28px;" />

                <!-- Heading -->
                <h1 style="margin:0 0 12px;font-size:20px;font-weight:700;color:#111827;">Password reset request</h1>

                <!-- Body text -->
                <p style="margin:0 0 24px;font-size:15px;line-height:1.65;color:#374151;">
                  Hello <strong>{safeName}</strong>,<br /><br />
                  An administrator has requested a password reset for your
                  <strong>LegalSynq</strong> account. Click the button below to
                  set a new password. This link expires in&nbsp;24&nbsp;hours.
                </p>

                <!-- CTA button -->
                <table role="presentation" cellpadding="0" cellspacing="0" style="margin-bottom:28px;">
                  <tr>
                    <td style="border-radius:8px;background:#f97316;">
                      <a href="{safeLink}"
                         style="display:inline-block;padding:13px 28px;font-size:15px;font-weight:600;
                                color:#ffffff;text-decoration:none;border-radius:8px;">
                        Reset my password
                      </a>
                    </td>
                  </tr>
                </table>

                <!-- Plain-text link fallback -->
                <p style="margin:0 0 4px;font-size:13px;color:#6b7280;">Or copy and paste this link into your browser:</p>
                <p style="margin:0 0 28px;font-size:13px;color:#f97316;word-break:break-all;">
                  <a href="{safeLink}" style="color:#f97316;">{safeLink}</a>
                </p>

                <hr style="border:none;border-top:1px solid #f3f4f6;margin:0 0 20px;" />

                <!-- Footer -->
                <p style="margin:0;font-size:13px;line-height:1.5;color:#9ca3af;">
                  If you didn&rsquo;t request a password reset, you can safely ignore
                  this email. Your password will not change until you follow the
                  link above.
                </p>

              </td>
            </tr>
          </table>
        </body>
        </html>
        """;
    }

    /// <summary>Minimal HTML encoding — avoids taking a System.Web dependency.</summary>
    private static string HtmlEncode(string value) =>
        value
            .Replace("&",  "&amp;")
            .Replace("<",  "&lt;")
            .Replace(">",  "&gt;")
            .Replace("\"", "&quot;")
            .Replace("'",  "&#39;");
}
