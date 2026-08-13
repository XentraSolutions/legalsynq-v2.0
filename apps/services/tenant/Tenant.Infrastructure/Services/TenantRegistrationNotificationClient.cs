using System.Net.Http.Json;
using System.Text.Encodings.Web;
using System.Text.Json;
using BuildingBlocks.Notifications;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Tenant.Application.Interfaces;

namespace Tenant.Infrastructure.Services;

public sealed class TenantRegistrationNotificationClient(
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration,
    ILogger<TenantRegistrationNotificationClient> logger) : ITenantRegistrationNotificationClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private const string SubmittedSubject = "We've received your LegalSynq tenant application";
    private const string DeclinedSubject = "Your LegalSynq tenant application was declined";

    public Task<(bool Success, string? Error)> SendSubmittedAsync(
        Guid registrationId, string toEmail, string displayName, string tenantName,
        CancellationToken ct = default) => SubmitAsync(
            registrationId, toEmail, NotificationTaxonomy.Tenant.Events.RegistrationSubmitted,
            SubmittedSubject,
            BuildSubmittedHtml(displayName, tenantName),
            $"We've received your LegalSynq tenant application\n\nHello {displayName},\n\nYour application for {tenantName} has been received and is now pending review. We will notify you when a decision has been made.",
            ct);

    public Task<(bool Success, string? Error)> SendDeclinedAsync(
        Guid registrationId, string toEmail, string displayName, string tenantName, string reason,
        CancellationToken ct = default) => SubmitAsync(
            registrationId, toEmail, NotificationTaxonomy.Tenant.Events.RegistrationDeclined,
            DeclinedSubject,
            BuildDeclinedHtml(displayName, tenantName),
            $"Your LegalSynq tenant application was declined\n\nHello {displayName},\n\nYour application for {tenantName} was declined.\n\nContact LegalSynq support if you have questions.",
            ct);

    private async Task<(bool Success, string? Error)> SubmitAsync(
        Guid registrationId, string toEmail, string eventKey, string subject,
        string html, string text, CancellationToken ct)
    {
        if (!configuration.GetValue("TenantRegistration:NotificationsEnabled", true))
            return (true, null);

        var baseUrl = configuration["NotificationsService:BaseUrl"];
        if (string.IsNullOrWhiteSpace(baseUrl))
            return (false, "NotificationsService:BaseUrl is not configured.");

        try
        {
            using var client = httpClientFactory.CreateClient("NotificationsService");
            client.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");

            var request = new NotificationsProducerRequest
            {
                Channel = NotificationTaxonomy.Channels.Email,
                ProductKey = NotificationTaxonomy.Tenant.ProductKey,
                EventKey = eventKey,
                SourceSystem = NotificationTaxonomy.Tenant.SourceSystem,
                Subject = subject,
                IdempotencyKey = $"{eventKey}:{registrationId:N}",
                Recipient = new NotificationsRecipient
                {
                    Email = toEmail,
                    TenantId = registrationId.ToString(),
                },
            Message = new
            {
                type = eventKey,
                subject,
                html,
                body = text,
                attachments = LegalSynqEmailBranding.CreateInlineLogoAttachment(),
            },
                TemplateData = new Dictionary<string, string>
                {
                    ["subject"] = subject,
                    ["registrationId"] = registrationId.ToString(),
                },
                Metadata = new Dictionary<string, string>
                {
                    ["registrationId"] = registrationId.ToString(),
                },
            };

            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "v1/notifications");
            httpRequest.Headers.Add("X-Tenant-Id", registrationId.ToString());
            httpRequest.Content = JsonContent.Create(request, options: JsonOptions);
            using var response = await client.SendAsync(httpRequest, ct);
            var responseBody = await response.Content.ReadAsStringAsync(ct);
            if (!response.IsSuccessStatusCode)
                return (false, $"Notifications service returned HTTP {(int)response.StatusCode}.");

            try
            {
                using var document = JsonDocument.Parse(responseBody);
                var status = document.RootElement.TryGetProperty("status", out var value) ? value.GetString() : null;
                return status == "sent"
                    ? (true, null)
                    : (false, $"Notification delivery finished with status '{status ?? "unknown"}'.");
            }
            catch (JsonException)
            {
                return (false, "Notifications service returned an invalid response.");
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Tenant registration notification {EventKey} failed for {RegistrationId}.", eventKey, registrationId);
            return (false, $"Notification delivery error: {ex.GetType().Name}.");
        }
    }

    private string BuildSubmittedHtml(string displayName, string tenantName) => BuildHtml(
        "Your application is under review",
        $"Hello <strong>{Encode(displayName)}</strong>,<br /><br />We&apos;ve received your application for <strong>{Encode(tenantName)}</strong>. It is now pending review, and we&apos;ll email you when a decision has been made.",
        "No further action is required at this time.");

    private string BuildDeclinedHtml(string displayName, string tenantName) => BuildHtml(
        "Your tenant application was declined",
        $"Hello <strong>{Encode(displayName)}</strong>,<br /><br />Your application for <strong>{Encode(tenantName)}</strong> was declined.",
        "Contact LegalSynq support if you have questions about this decision.");

    private string BuildHtml(string heading, string content, string footer)
    {
        return $"""
        <!DOCTYPE html>
        <html lang="en"><head><meta charset="utf-8" /><meta name="viewport" content="width=device-width,initial-scale=1.0" /><title>{Encode(heading)}</title></head>
        <body style="margin:0;padding:32px 16px;background:#f9fafb;font-family:-apple-system,BlinkMacSystemFont,'Segoe UI',Roboto,Helvetica,Arial,sans-serif;">
          <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="max-width:520px;margin:0 auto;"><tr><td style="background:#ffffff;border-radius:12px;padding:40px;border:1px solid #e5e7eb;">
            <img src="{LegalSynqEmailBranding.LogoSource}" width="145" alt="LegalSynq" style="display:block;width:145px;height:auto;border:0;margin:0 auto 16px;" /><hr style="border:none;border-top:1px solid #f3f4f6;margin:0 0 28px;" />
            <h1 style="margin:0 0 12px;font-size:20px;line-height:1.4;font-weight:700;color:#111827;">{Encode(heading)}</h1>
            <p style="margin:0 0 24px;font-size:15px;line-height:1.65;color:#374151;">{content}</p>
            <hr style="border:none;border-top:1px solid #f3f4f6;margin:0 0 20px;" />
            <p style="margin:0;font-size:13px;line-height:1.5;color:#9ca3af;">{footer}</p>
          </td></tr></table>
        </body></html>
        """;
    }

    private static string Encode(string value) => HtmlEncoder.Default.Encode(value);
}
