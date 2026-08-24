using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Tenant.Infrastructure.Services;
using Xunit;

namespace Tenant.Application.Tests;

public sealed class TenantRegistrationNotificationClientTests
{
    [Fact]
    public async Task Submitted_and_declined_emails_use_dedicated_branded_events()
    {
        var handler = new CaptureHandler();
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["TenantRegistration:NotificationsEnabled"] = "true",
            ["NotificationsService:BaseUrl"] = "https://notifications.example.test",
        }).Build();
        var client = new TenantRegistrationNotificationClient(
            new TestHttpClientFactory(handler), configuration,
            NullLogger<TenantRegistrationNotificationClient>.Instance);
        var registrationId = Guid.Parse("20000000-0000-0000-0000-000000000001");

        var submitted = await client.SendSubmittedAsync(
            registrationId, "jane@example.test", "Jane Doe", "Sterling Associates");
        var declined = await client.SendDeclinedAsync(
            registrationId, "jane@example.test", "Jane Doe", "Sterling Associates", "Incomplete documentation");

        Assert.True(submitted.Success);
        Assert.True(declined.Success);
        Assert.Equal(2, handler.Bodies.Count);

        using var submittedPayload = JsonDocument.Parse(handler.Bodies[0]);
        Assert.Equal("tenant.registration.submitted", submittedPayload.RootElement.GetProperty("eventKey").GetString());
        var submittedHtml = submittedPayload.RootElement.GetProperty("message").GetProperty("html").GetString()!;
        Assert.Contains("Your application is under review", submittedHtml);
        Assert.Contains("src=\"cid:legalsynq-logo\"", submittedHtml);
        Assert.Contains("margin:0 auto 16px", submittedHtml);
        AssertInlineLogo(submittedPayload.RootElement.GetProperty("message"));

        using var declinedPayload = JsonDocument.Parse(handler.Bodies[1]);
        Assert.Equal("tenant.registration.declined", declinedPayload.RootElement.GetProperty("eventKey").GetString());
        var declinedHtml = declinedPayload.RootElement.GetProperty("message").GetProperty("html").GetString()!;
        var declinedText = declinedPayload.RootElement.GetProperty("message").GetProperty("body").GetString()!;
        Assert.Contains("Your tenant application was declined", declinedHtml);
        Assert.DoesNotContain("Reason:", declinedHtml);
        Assert.DoesNotContain("Incomplete documentation", declinedHtml);
        Assert.DoesNotContain("Reason:", declinedText);
        Assert.DoesNotContain("Incomplete documentation", declinedText);
        Assert.Contains("src=\"cid:legalsynq-logo\"", declinedHtml);
        Assert.Contains("margin:0 auto 16px", declinedHtml);
        AssertInlineLogo(declinedPayload.RootElement.GetProperty("message"));
    }

    private static void AssertInlineLogo(JsonElement message)
    {
        var attachment = Assert.Single(message.GetProperty("attachments").EnumerateArray());
        Assert.Equal("legalsynq-logo", attachment.GetProperty("contentId").GetString());
        Assert.Equal("inline", attachment.GetProperty("disposition").GetString());
        Assert.Equal("image/png", attachment.GetProperty("type").GetString());
        Assert.NotEmpty(attachment.GetProperty("content").GetString()!);
    }

    private sealed class TestHttpClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }

    private sealed class CaptureHandler : HttpMessageHandler
    {
        public List<string> Bodies { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Bodies.Add(await request.Content!.ReadAsStringAsync(cancellationToken));
            return new HttpResponseMessage(HttpStatusCode.Accepted)
            {
                Content = new StringContent("{\"status\":\"sent\"}"),
            };
        }
    }
}
