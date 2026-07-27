using System.Net;
using System.Text;
using System.Text.Json;
using Liens.Application.Interfaces;
using Liens.Infrastructure.Identity;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Liens.Api.Tests.Tests;

public sealed class IdentityBuyerAccountProvisioningServiceTests
{
    [Fact]
    public async Task ProvisionBuyerAccountAsync_ensures_identity_org_before_self_registering_user()
    {
        var identityOrgId = Guid.Parse("30000000-0000-0000-0000-000000000301");
        var userId = Guid.Parse("30000000-0000-0000-0000-000000000302");
        var handler = new CapturingIdentityHandler(identityOrgId, userId);
        var sut = CreateSut(handler);

        var result = await sut.ProvisionBuyerAccountAsync(
            new PublicBuyerAccountProvisioningRequest(
                Guid.Parse("10000000-0000-0000-0000-000000000001"),
                Guid.Parse("40000000-0000-0000-0000-000000000012"),
                "Capital Fund LLC",
                "buyer@capital.test",
                "Password123!",
                "Buyer",
                "Reviewer",
                "+13105551212"));

        result.Success.Should().BeTrue();
        result.UserId.Should().Be(userId);
        handler.Paths.Should().Equal(
            "/api/admin/organizations/synqlien-buyer",
            $"/api/admin/organizations/{identityOrgId}/synqlien-buyer-self-register");

        using var ensureBody = JsonDocument.Parse(handler.Bodies[0]);
        ensureBody.RootElement.GetProperty("tenantId").GetGuid()
            .Should().Be(Guid.Parse("10000000-0000-0000-0000-000000000001"));
        ensureBody.RootElement.GetProperty("sourceBuyerOrgId").GetGuid()
            .Should().Be(Guid.Parse("40000000-0000-0000-0000-000000000012"));
        ensureBody.RootElement.GetProperty("buyerCompanyName").GetString()
            .Should().Be("Capital Fund LLC");
        ensureBody.RootElement.GetProperty("contactEmail").GetString()
            .Should().Be("buyer@capital.test");

        using var registerBody = JsonDocument.Parse(handler.Bodies[1]);
        registerBody.RootElement.GetProperty("tenantId").GetGuid()
            .Should().Be(Guid.Parse("10000000-0000-0000-0000-000000000001"));
        registerBody.RootElement.GetProperty("email").GetString()
            .Should().Be("buyer@capital.test");
        registerBody.RootElement.GetProperty("phone").GetString()
            .Should().Be("+13105551212");
    }

    [Fact]
    public async Task ProvisionBuyerAccountAsync_rejects_identity_existing_account_success_response()
    {
        var handler = new CapturingIdentityHandler(
            Guid.Parse("30000000-0000-0000-0000-000000000301"),
            Guid.Parse("30000000-0000-0000-0000-000000000302"),
            selfRegisterIsNew: false);
        var sut = CreateSut(handler);

        var result = await sut.ProvisionBuyerAccountAsync(
            new PublicBuyerAccountProvisioningRequest(
                Guid.Parse("10000000-0000-0000-0000-000000000001"),
                Guid.Parse("40000000-0000-0000-0000-000000000012"),
                "Capital Fund LLC",
                "buyer@capital.test",
                "Password123!",
                "Buyer",
                "Reviewer",
                "+13105551212"));

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be("account-conflict");
        result.StatusCode.Should().Be((int)HttpStatusCode.Conflict);
        result.ErrorMessage.Should().Be(
            "An account with this email already exists. Log in with your existing account instead.");
    }

    [Fact]
    public async Task ProvisionBuyerAccountAsync_preserves_identity_existing_account_conflict()
    {
        var handler = new CapturingIdentityHandler(
            Guid.Parse("30000000-0000-0000-0000-000000000301"),
            Guid.Parse("30000000-0000-0000-0000-000000000302"),
            selfRegisterStatus: HttpStatusCode.Conflict,
            selfRegisterErrorJson:
                """{"error":"An account with this email already exists. Log in with your existing account instead.","code":"ACCOUNT_ALREADY_EXISTS"}""");
        var sut = CreateSut(handler);

        var result = await sut.ProvisionBuyerAccountAsync(
            new PublicBuyerAccountProvisioningRequest(
                Guid.Parse("10000000-0000-0000-0000-000000000001"),
                Guid.Parse("40000000-0000-0000-0000-000000000012"),
                "Capital Fund LLC",
                "buyer@capital.test",
                "Password123!",
                "Buyer",
                "Reviewer",
                "+13105551212"));

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be("account_already_exists");
        result.StatusCode.Should().Be((int)HttpStatusCode.Conflict);
        result.ErrorMessage.Should().Be(
            "An account with this email already exists. Log in with your existing account instead.");
    }

    private static IdentityBuyerAccountProvisioningService CreateSut(
        CapturingIdentityHandler handler)
    {
        var client = new HttpClient(handler);
        return new IdentityBuyerAccountProvisioningService(
            new SingleClientFactory(client),
            Options.Create(new IdentityServiceOptions
            {
                BaseUrl = "http://identity.test",
                TimeoutSeconds = 5,
            }),
            NullLogger<IdentityBuyerAccountProvisioningService>.Instance);
    }

    private sealed class SingleClientFactory(HttpClient client) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => client;
    }

    private sealed class CapturingIdentityHandler(
        Guid identityOrgId,
        Guid userId,
        bool selfRegisterIsNew = true,
        HttpStatusCode selfRegisterStatus = HttpStatusCode.OK,
        string? selfRegisterErrorJson = null) : HttpMessageHandler
    {
        public List<string> Paths { get; } = [];
        public List<string> Bodies { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Paths.Add(request.RequestUri!.AbsolutePath);
            Bodies.Add(await request.Content!.ReadAsStringAsync(cancellationToken));

            if (request.RequestUri.AbsolutePath == "/api/admin/organizations/synqlien-buyer")
            {
                return JsonResponse($$"""{"id":"{{identityOrgId}}","name":"Capital Fund LLC","isNew":true}""");
            }

            if (request.RequestUri.AbsolutePath == $"/api/admin/organizations/{identityOrgId}/synqlien-buyer-self-register")
            {
                if (selfRegisterStatus != HttpStatusCode.OK)
                {
                    return new HttpResponseMessage(selfRegisterStatus)
                    {
                        Content = new StringContent(
                            selfRegisterErrorJson ?? """{"error":"Account activation failed.","code":"IDENTITY_ERROR"}""",
                            Encoding.UTF8,
                            "application/json"),
                    };
                }

                return JsonResponse($$"""{"userId":"{{userId}}","isNew":{{selfRegisterIsNew.ToString().ToLowerInvariant()}}}""");
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        }

        private static HttpResponseMessage JsonResponse(string json)
            => new(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json"),
            };
    }
}
