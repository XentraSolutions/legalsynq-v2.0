using System.Net;
using System.Text;
using BuildingBlocks.Authentication.ServiceTokens;
using BuildingBlocks.Context;
using CareConnect.Infrastructure.Documents;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace CareConnect.Tests.Infrastructure;

public class DocumentServiceClientTests
{
    [Fact]
    public async Task UploadAsync_MintsDocumentsServiceToken_WhenIssuerConfigured()
    {
        var tenantId = Guid.NewGuid();
        var actorUserId = Guid.NewGuid();
        var handler = new CapturingHandler("""
{"data":{"id":"doc-123"}}
""");
        var issuer = new Mock<IServiceTokenIssuer>();
        issuer.SetupGet(x => x.IsConfigured).Returns(true);
        issuer.Setup(x => x.IssueToken(tenantId.ToString(), actorUserId.ToString(), "documents-service"))
            .Returns("minted-jwt");

        var sut = CreateSut(handler, issuer.Object, tenantId, actorUserId);

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("hello"));
        var result = await sut.UploadAsync(
            stream,
            "hello.txt",
            "text/plain",
            stream.Length,
            tenantId,
            "hello.txt");

        Assert.True(result.Success);
        Assert.Equal("Bearer minted-jwt", handler.AuthorizationHeader);
        issuer.VerifyAll();
    }

    [Fact]
    public async Task GetSignedUrlAsync_FallsBackToLegacyToken_WhenIssuerUnavailable()
    {
        var tenantId = Guid.NewGuid();
        var handler = new CapturingHandler("""
{"data":{"redeemUrl":"https://example.test/file","expiresInSeconds":300}}
""");
        var issuer = new Mock<IServiceTokenIssuer>();
        issuer.SetupGet(x => x.IsConfigured).Returns(false);

        var sut = CreateSut(
            handler,
            issuer.Object,
            tenantId,
            userId: null,
            legacyToken: "legacy-token");

        var result = await sut.GetSignedUrlAsync(tenantId, "doc-123", false);

        Assert.NotNull(result);
        Assert.Equal("Bearer legacy-token", handler.AuthorizationHeader);
    }

    [Fact]
    public async Task GetSignedUrlAsync_UsesPublicAccessBaseUrl_ForRelativeRedeemUrls()
    {
        var tenantId = Guid.NewGuid();
        var handler = new CapturingHandler("""
{"data":{"redeemUrl":"/access/token-123","expiresInSeconds":300}}
""");
        var issuer = new Mock<IServiceTokenIssuer>();
        issuer.SetupGet(x => x.IsConfigured).Returns(false);

        var sut = CreateSut(
            handler,
            issuer.Object,
            tenantId,
            userId: null,
            legacyToken: "legacy-token",
            publicBaseUrl: "/api/documents");

        var result = await sut.GetSignedUrlAsync(tenantId, "doc-123", false);

        Assert.NotNull(result);
        Assert.Equal("/api/documents/access/token-123", result!.RedeemUrl);
    }

    [Fact]
    public async Task GetSignedUrlAsync_MintsDocumentsServiceToken_FromExplicitTenantId_WhenRequestContextHasNoTenant()
    {
        var tenantId = Guid.NewGuid();
        var handler = new CapturingHandler("""
{"data":{"redeemUrl":"https://example.test/file","expiresInSeconds":300}}
""");
        var issuer = new Mock<IServiceTokenIssuer>();
        issuer.SetupGet(x => x.IsConfigured).Returns(true);
        issuer.Setup(x => x.IssueToken(tenantId.ToString(), null, "documents-service"))
            .Returns("minted-jwt");

        var sut = CreateSut(handler, issuer.Object, tenantId: null, userId: null);

        var result = await sut.GetSignedUrlAsync(tenantId, "doc-123", false);

        Assert.NotNull(result);
        Assert.Equal("Bearer minted-jwt", handler.AuthorizationHeader);
        issuer.VerifyAll();
    }

    private static DocumentServiceClient CreateSut(
        CapturingHandler handler,
        IServiceTokenIssuer issuer,
        Guid? tenantId,
        Guid? userId,
        string legacyToken = "",
        string? publicBaseUrl = null)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DocumentsService:BaseUrl"] = "http://documents.test",
                ["DocumentsService:PublicBaseUrl"] = publicBaseUrl,
                ["DocumentsService:LegacyToken"] = null,
                ["DocumentsService:ServiceToken"] = legacyToken,
                ["DocumentsService:DocumentTypeId"] = Guid.NewGuid().ToString(),
            })
            .Build();

        var client = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://documents.test")
        };

        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(x => x.CreateClient("DocumentsService")).Returns(client);

        var requestContext = new Mock<ICurrentRequestContext>();
        requestContext.SetupGet(x => x.TenantId).Returns(tenantId);
        requestContext.SetupGet(x => x.UserId).Returns(userId);

        return new DocumentServiceClient(
            factory.Object,
            config,
            issuer,
            requestContext.Object,
            NullLogger<DocumentServiceClient>.Instance);
    }

    private sealed class CapturingHandler : HttpMessageHandler
    {
        private readonly string _body;

        public CapturingHandler(string body)
        {
            _body = body;
        }

        public string? AuthorizationHeader { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            AuthorizationHeader = request.Headers.Authorization?.ToString();
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(_body, Encoding.UTF8, "application/json")
            });
        }
    }
}
