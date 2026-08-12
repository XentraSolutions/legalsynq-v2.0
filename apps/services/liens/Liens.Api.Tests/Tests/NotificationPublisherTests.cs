using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Liens.Application.Interfaces;
using Liens.Infrastructure.Notifications;
using Microsoft.Extensions.Logging.Abstractions;

namespace Liens.Api.Tests.Tests;

public class NotificationPublisherTests
{
    [Fact]
    public async Task SendEmailAsync_serializes_html_and_plain_text_bodies_separately()
    {
        var handler = new CapturingHandler();
        var publisher = CreatePublisher(handler);

        await publisher.SendEmailAsync(
            "liens.selling.submitted",
            Guid.CreateVersion7(),
            "buyer.reviewer@capital.test",
            "New Lien Offer",
            "Plain text body",
            new Dictionary<string, string> { ["lienId"] = Guid.CreateVersion7().ToString() },
            CancellationToken.None,
            new NotificationEmailSendOptions(
                TemplateKey: "lien-selling-submitted-email",
                HtmlBody: "<!doctype html><html><body><p>Rendered offer</p></body></html>",
                TextBody: "Plain text body"));

        using var payload = JsonDocument.Parse(handler.RequestBody!);
        var root = payload.RootElement;
        var message = root.GetProperty("message");

        root.GetProperty("channel").GetString().Should().Be("email");
        root.GetProperty("templateKey").GetString().Should().Be("lien-selling-submitted-email");
        message.GetProperty("subject").GetString().Should().Be("New Lien Offer");
        message.GetProperty("body").GetString().Should().Be("Plain text body");
        message.GetProperty("html").GetString().Should().Be("<!doctype html><html><body><p>Rendered offer</p></body></html>");
    }

    [Fact]
    public async Task SendEmailAsync_moves_legacy_html_body_to_html_message_field()
    {
        var handler = new CapturingHandler();
        var publisher = CreatePublisher(handler);
        const string htmlBody = "<!doctype html><html><body><p>Rendered offer</p></body></html>";

        await publisher.SendEmailAsync(
            "liens.selling.submitted",
            Guid.CreateVersion7(),
            "buyer.reviewer@capital.test",
            "New Lien Offer",
            htmlBody,
            new Dictionary<string, string>(),
            CancellationToken.None);

        using var payload = JsonDocument.Parse(handler.RequestBody!);
        var message = payload.RootElement.GetProperty("message");

        message.GetProperty("html").GetString().Should().Be(htmlBody);
        message.GetProperty("body").GetString().Should().Be("Please view this email in an HTML-capable mail client.");
    }

    [Fact]
    public async Task SendEmailAsync_serializes_inline_attachments_for_cid_images()
    {
        var handler = new CapturingHandler();
        var publisher = CreatePublisher(handler);

        await publisher.SendEmailAsync(
            "liens.selling.submitted",
            Guid.CreateVersion7(),
            "buyer.reviewer@capital.test",
            "New Lien Offer",
            "Plain text body",
            new Dictionary<string, string>(),
            CancellationToken.None,
            new NotificationEmailSendOptions(
                HtmlBody: "<!doctype html><html><body><img src=\"cid:legalsynq-brand-icon\"></body></html>",
                TextBody: "Plain text body",
                InlineAttachments:
                [
                    new NotificationEmailInlineAttachment(
                        "legalsynq-brand-icon",
                        "legalsynq-brand-icon.png",
                        "image/png",
                        "aW1hZ2U="),
                ]));

        using var payload = JsonDocument.Parse(handler.RequestBody!);
        var attachments = payload.RootElement
            .GetProperty("message")
            .GetProperty("attachments");

        attachments.GetArrayLength().Should().Be(1);
        var attachment = attachments[0];
        attachment.GetProperty("contentId").GetString().Should().Be("legalsynq-brand-icon");
        attachment.GetProperty("filename").GetString().Should().Be("legalsynq-brand-icon.png");
        attachment.GetProperty("type").GetString().Should().Be("image/png");
        attachment.GetProperty("content").GetString().Should().Be("aW1hZ2U=");
        attachment.GetProperty("disposition").GetString().Should().Be("inline");
    }

    [Fact]
    public async Task SendEmailAsync_serializes_disable_click_tracking_for_public_cta()
    {
        var handler = new CapturingHandler();
        var publisher = CreatePublisher(handler);

        await publisher.SendEmailAsync(
            "liens.selling.submitted",
            Guid.CreateVersion7(),
            "buyer.reviewer@capital.test",
            "New Lien Offer",
            "View Lien for Sale: https://tenant.legalsynq.test/selling/public/token",
            new Dictionary<string, string>(),
            CancellationToken.None,
            new NotificationEmailSendOptions(
                HtmlBody: "<!doctype html><html><body><a href=\"https://tenant.legalsynq.test/selling/public/token\">View Lien for Sale</a></body></html>",
                TextBody: "View Lien for Sale: https://tenant.legalsynq.test/selling/public/token",
                DisableClickTracking: true));

        using var payload = JsonDocument.Parse(handler.RequestBody!);
        var message = payload.RootElement.GetProperty("message");

        message.GetProperty("disableClickTracking").GetBoolean().Should().BeTrue();
    }

    private static NotificationPublisher CreatePublisher(CapturingHandler handler)
    {
        var client = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://notifications.test"),
        };

        return new NotificationPublisher(
            new SingleClientFactory(client),
            NullLogger<NotificationPublisher>.Instance);
    }

    private sealed class SingleClientFactory(HttpClient client) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => client;
    }

    private sealed class CapturingHandler : HttpMessageHandler
    {
        public string? RequestBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestBody = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new
                {
                    id = Guid.CreateVersion7(),
                    status = "sent",
                    blockedByPolicy = false,
                    blockedReasonCode = (string?)null,
                    failureCategory = (string?)null,
                    lastErrorMessage = (string?)null,
                }),
            };
        }
    }
}
