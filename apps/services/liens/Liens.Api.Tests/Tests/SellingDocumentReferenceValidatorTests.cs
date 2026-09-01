using BuildingBlocks.Authentication.ServiceTokens;
using BuildingBlocks.Exceptions;
using Liens.Infrastructure.Documents;
using Microsoft.Extensions.Logging.Abstractions;

namespace Liens.Api.Tests.Tests;

public sealed class SellingDocumentReferenceValidatorTests
{
    [Fact]
    public async Task IsAccessibleAsync_WhenDocumentsServiceTimesOut_ReturnsActionableServiceUnavailableError()
    {
        using var client = new HttpClient(new ThrowingHandler(new TaskCanceledException("Request timed out")))
        {
            BaseAddress = new Uri("https://documents.test"),
        };
        var validator = new SellingDocumentReferenceValidator(
            new SingleClientFactory(client),
            new DisabledServiceTokenIssuer(),
            NullLogger<SellingDocumentReferenceValidator>.Instance);

        var action = () => validator.IsAccessibleAsync(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            null,
            Guid.NewGuid());

        await action.Should().ThrowAsync<ServiceUnavailableException>()
            .WithMessage("The document was uploaded, but verification timed out before it could be attached to this lien. Please try again.");
    }

    private sealed class SingleClientFactory(HttpClient client) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => client;
    }

    private sealed class DisabledServiceTokenIssuer : IServiceTokenIssuer
    {
        public bool IsConfigured => false;

        public string IssueToken(string tenantId, string? actorUserId = null, string? audience = null) =>
            throw new InvalidOperationException("Token issuance should not be requested when disabled.");
    }

    private sealed class ThrowingHandler(Exception exception) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) => Task.FromException<HttpResponseMessage>(exception);
    }
}
