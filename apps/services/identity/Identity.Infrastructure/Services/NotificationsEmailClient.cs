using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using BuildingBlocks.Notifications;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Identity.Infrastructure.Services;

/// <summary>
/// LS-NOTIF-CORE-024: Sends transactional identity emails via the canonical
/// Notifications service endpoint (<c>POST /v1/notifications</c>).
///
/// <para>
/// Replaces the old <c>POST /internal/send-email</c> passthrough. Identity
/// is now a first-class Notifications producer:
/// <list type="bullet">
///   <item><c>productKey = "identity"</c>, <c>sourceSystem = "identity-service"</c></item>
///   <item><c>eventKey</c> = <c>identity.user.password.reset</c> or <c>identity.user.invite.sent</c></item>
///   <item>Auth via <c>NotificationsAuthDelegatingHandler</c> (service JWT when configured,
///         legacy <c>X-Tenant-Id</c> fallback otherwise)</item>
/// </list>
/// </para>
///
/// <para>
/// Identity remains responsible for token generation, link construction, and
/// inline HTML rendering. Notifications handles delivery, retry, provider
/// routing, and observability.
/// </para>
///
/// <para>
/// When <c>NotificationsService:BaseUrl</c> is not configured, both methods
/// return <c>EmailConfigured = false</c> so callers can apply the appropriate
/// fallback without treating it as a delivery failure.
/// </para>
/// </summary>
public interface INotificationsEmailClient
{
    /// <summary>
    /// Dispatches a password-reset email to <paramref name="toEmail"/>.
    /// </summary>
    Task<(bool EmailConfigured, bool Success, string? Error)> SendPasswordResetEmailAsync(
        string            toEmail,
        string            displayName,
        string            resetLink,
        Guid              tenantId,
        CancellationToken ct = default);

    /// <summary>
    /// LS-ID-TNT-007: Dispatches a user-invitation email to <paramref name="toEmail"/>.
    /// Contains the activation link the invitee uses to set their password and
    /// activate their account via <c>POST /api/auth/accept-invite</c>.
    /// </summary>
    Task<(bool EmailConfigured, bool Success, string? Error)> SendInviteEmailAsync(
        string            toEmail,
        string            displayName,
        string            activationLink,
        Guid              tenantId,
        CancellationToken ct = default);
}

public sealed class NotificationsEmailClient : INotificationsEmailClient
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private const string PasswordResetEventKey = "identity.user.password.reset";
    private const string InviteEventKey        = "identity.user.invite.sent";
    private const string PasswordResetSubject  = "Reset your LegalSynq password";
    private const string InviteSubject         = "You've been invited to LegalSynq";

    private readonly IHttpClientFactory                _httpClientFactory;
    private readonly NotificationsServiceOptions       _options;
    private readonly ILogger<NotificationsEmailClient> _logger;

    public NotificationsEmailClient(
        IHttpClientFactory                    httpClientFactory,
        IOptions<NotificationsServiceOptions> options,
        ILogger<NotificationsEmailClient>     logger)
    {
        _httpClientFactory = httpClientFactory;
        _options           = options.Value;
        _logger            = logger;
    }

    public Task<(bool EmailConfigured, bool Success, string? Error)> SendPasswordResetEmailAsync(
        string            toEmail,
        string            displayName,
        string            resetLink,
        Guid              tenantId,
        CancellationToken ct = default)
    {
        var body = new
        {
            type    = PasswordResetEventKey,
            subject = PasswordResetSubject,
            body    = BuildPasswordResetHtmlBody(displayName, resetLink),
        };

        var templateData = new Dictionary<string, string>
        {
            ["displayName"] = displayName,
            ["resetLink"]   = resetLink,
            ["subject"]     = PasswordResetSubject,
        };

        return SubmitAsync(
            eventKey:     PasswordResetEventKey,
            subject:      PasswordResetSubject,
            toEmail:      toEmail,
            tenantId:     tenantId,
            body:         body,
            templateData: templateData,
            logTag:       "LS-NOTIF-CORE-024/password-reset",
            ct:           ct);
    }

    public Task<(bool EmailConfigured, bool Success, string? Error)> SendInviteEmailAsync(
        string            toEmail,
        string            displayName,
        string            activationLink,
        Guid              tenantId,
        CancellationToken ct = default)
    {
        var body = new
        {
            type    = InviteEventKey,
            subject = InviteSubject,
            body    = BuildInviteHtmlBody(displayName, activationLink),
        };

        var templateData = new Dictionary<string, string>
        {
            ["displayName"]    = displayName,
            ["activationLink"] = activationLink,
            ["subject"]        = InviteSubject,
        };

        return SubmitAsync(
            eventKey:     InviteEventKey,
            subject:      InviteSubject,
            toEmail:      toEmail,
            tenantId:     tenantId,
            body:         body,
            templateData: templateData,
            logTag:       "LS-NOTIF-CORE-024/invite",
            ct:           ct);
    }

    // ── Core submission ───────────────────────────────────────────────────────

    private async Task<(bool EmailConfigured, bool Success, string? Error)> SubmitAsync(
        string            eventKey,
        string            subject,
        string            toEmail,
        Guid              tenantId,
        object            body,
        Dictionary<string, string> templateData,
        string            logTag,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_options.BaseUrl))
        {
            _logger.LogDebug(
                "[{Tag}] NotificationsService:BaseUrl not configured; " +
                "email for {Email} will use the non-production fallback.",
                logTag, toEmail);
            return (EmailConfigured: false, Success: false, Error: null);
        }

        try
        {
            using var client = _httpClientFactory.CreateClient("NotificationsService");
            client.BaseAddress = new Uri(_options.BaseUrl.TrimEnd('/') + "/");
            client.Timeout     = TimeSpan.FromSeconds(_options.TimeoutSeconds);

            var request = new NotificationsProducerRequest
            {
                Channel        = NotificationTaxonomy.Channels.Email,
                ProductKey     = NotificationTaxonomy.Identity.ProductKey,
                EventKey       = eventKey,
                SourceSystem   = NotificationTaxonomy.Identity.SourceSystem,
                IdempotencyKey = Guid.NewGuid().ToString("N"),
                Recipient      = new NotificationsRecipient
                {
                    Email    = toEmail,
                    TenantId = tenantId.ToString(),
                },
                Message        = body,
                TemplateData   = templateData,
                Metadata       = new Dictionary<string, string>
                {
                    ["tenantId"] = tenantId.ToString(),
                },
            };

            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "v1/notifications");
            httpRequest.Headers.Add("X-Tenant-Id", tenantId.ToString());
            httpRequest.Content = JsonContent.Create(request, options: JsonOpts);

            using var response = await client.SendAsync(httpRequest, ct);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation(
                    "[{Tag}] Email dispatched to {Email} (tenant={TenantId}).",
                    logTag, toEmail, tenantId);
                return (EmailConfigured: true, Success: true, Error: null);
            }

            var responseBody = await response.Content.ReadAsStringAsync(ct);
            _logger.LogWarning(
                "[{Tag}] Notifications service returned HTTP {Status} for {Email} (tenant={TenantId}). Body: {Body}",
                logTag, (int)response.StatusCode, toEmail, tenantId, responseBody);
            return (
                EmailConfigured: true,
                Success:         false,
                Error:           $"Notifications service returned HTTP {(int)response.StatusCode}.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "[{Tag}] Email dispatch threw for {Email} (tenant={TenantId}).",
                logTag, toEmail, tenantId);
            return (
                EmailConfigured: true,
                Success:         false,
                Error:           $"Email delivery error: {ex.GetType().Name}.");
        }
    }

    // ── Inline HTML templates ─────────────────────────────────────────────────
    // Identity owns template rendering until tenant templates are registered
    // in the Notifications template catalog (LS-NOTIF-CORE-022 follow-up).

    private static string BuildPasswordResetHtmlBody(string name, string link)
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

    private static string BuildInviteHtmlBody(string name, string link)
    {
        var safeName = HtmlEncode(name);
        var safeLink = HtmlEncode(link);

        return $"""
        <!DOCTYPE html>
        <html lang="en">
        <head>
          <meta charset="utf-8" />
          <meta name="viewport" content="width=device-width, initial-scale=1.0" />
          <title>You've been invited to LegalSynq</title>
        </head>
        <body style="margin:0;padding:32px 0;background:#f9fafb;font-family:-apple-system,BlinkMacSystemFont,'Segoe UI',Roboto,Helvetica,Arial,sans-serif;">
          <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="max-width:520px;margin:0 auto;">
            <tr>
              <td style="background:#ffffff;border-radius:12px;padding:40px;border:1px solid #e5e7eb;">

                <!-- Wordmark -->
                <p style="margin:0 0 4px;font-size:22px;font-weight:700;color:#111827;letter-spacing:-0.3px;">LegalSynq</p>
                <hr style="border:none;border-top:1px solid #f3f4f6;margin:16px 0 28px;" />

                <!-- Heading -->
                <h1 style="margin:0 0 12px;font-size:20px;font-weight:700;color:#111827;">You've been invited</h1>

                <!-- Body text -->
                <p style="margin:0 0 24px;font-size:15px;line-height:1.65;color:#374151;">
                  Hello <strong>{safeName}</strong>,<br /><br />
                  An administrator has invited you to join <strong>LegalSynq</strong>.
                  Click the button below to set your password and activate your account.
                  This link expires in&nbsp;72&nbsp;hours.
                </p>

                <!-- CTA button -->
                <table role="presentation" cellpadding="0" cellspacing="0" style="margin-bottom:28px;">
                  <tr>
                    <td style="border-radius:8px;background:#f97316;">
                      <a href="{safeLink}"
                         style="display:inline-block;padding:13px 28px;font-size:15px;font-weight:600;
                                color:#ffffff;text-decoration:none;border-radius:8px;">
                        Accept invitation
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
                  If you weren&rsquo;t expecting this invitation, you can safely ignore
                  this email. No account will be created unless you follow the link above.
                </p>

              </td>
            </tr>
          </table>
        </body>
        </html>
        """;
    }

    private static string HtmlEncode(string value) =>
        value
            .Replace("&",  "&amp;")
            .Replace("<",  "&lt;")
            .Replace(">",  "&gt;")
            .Replace("\"", "&quot;")
            .Replace("'",  "&#39;");
}
