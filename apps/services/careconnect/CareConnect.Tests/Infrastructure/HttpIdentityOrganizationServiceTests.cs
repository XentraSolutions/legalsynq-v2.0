using System.Net;
using System.Text;
using System.Text.Json;
using CareConnect.Infrastructure.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace CareConnect.Tests.Infrastructure;

public class HttpIdentityOrganizationServiceTests
{
    [Fact]
    public async Task RegisterUserDirectlyAsync_SendsPhoneInSelfRegisterPayload()
    {
        var handler = new CapturingHandler("""{"userId":"0f7eb0fa-5d7f-4778-b0c5-0a5f260cb2f4","isNew":true}""");
        var sut = CreateSut(handler);

        _ = await sut.RegisterUserDirectlyAsync(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            Guid.Parse("22222222-2222-2222-2222-222222222222"),
            "user@example.com",
            "Password123!",
            "Casey",
            "Referrer",
            "+15551234567",
            CancellationToken.None);

        Assert.NotNull(handler.JsonBody);
        Assert.Equal("+15551234567", handler.JsonBody!.RootElement.GetProperty("phone").GetString());
    }

    [Fact]
    public async Task RegisterUserDirectlyAsync_SendsNullPhoneWhenMissing()
    {
        var handler = new CapturingHandler("""{"userId":"0f7eb0fa-5d7f-4778-b0c5-0a5f260cb2f4","isNew":false}""");
        var sut = CreateSut(handler);

        _ = await sut.RegisterUserDirectlyAsync(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            Guid.Parse("22222222-2222-2222-2222-222222222222"),
            "user@example.com",
            "Password123!",
            "Casey",
            "Referrer",
            null,
            CancellationToken.None);

        Assert.NotNull(handler.JsonBody);
        Assert.True(handler.JsonBody!.RootElement.TryGetProperty("phone", out var phoneProp));
        Assert.Equal(JsonValueKind.Null, phoneProp.ValueKind);
    }

    [Fact]
    public async Task RegisterUserDirectlyAsync_SendsTitleInSelfRegisterPayload()
    {
        var handler = new CapturingHandler("""{"userId":"0f7eb0fa-5d7f-4778-b0c5-0a5f260cb2f4","isNew":true}""");
        var sut = CreateSut(handler);

        _ = await sut.RegisterUserDirectlyAsync(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            Guid.Parse("22222222-2222-2222-2222-222222222222"),
            "user@example.com",
            "Password123!",
            "Casey",
            "Referrer",
            "+15551234567",
            CancellationToken.None,
            title: "Dr.");

        Assert.NotNull(handler.JsonBody);
        Assert.Equal("Dr.", handler.JsonBody!.RootElement.GetProperty("title").GetString());
    }

    private static HttpIdentityOrganizationService CreateSut(CapturingHandler handler)
    {
        var client = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://identity.test/")
        };

        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(x => x.CreateClient("IdentityService")).Returns(client);

        return new HttpIdentityOrganizationService(
            factory.Object,
            Options.Create(new IdentityServiceOptions
            {
                BaseUrl = "http://identity.test",
                TimeoutSeconds = 5,
            }),
            NullLogger<HttpIdentityOrganizationService>.Instance);
    }

    private sealed class CapturingHandler(string responseBody) : HttpMessageHandler
    {
        public JsonDocument? JsonBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = await request.Content!.ReadAsStringAsync(cancellationToken);
            JsonBody = JsonDocument.Parse(body);

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseBody, Encoding.UTF8, "application/json")
            };
        }
    }
}
