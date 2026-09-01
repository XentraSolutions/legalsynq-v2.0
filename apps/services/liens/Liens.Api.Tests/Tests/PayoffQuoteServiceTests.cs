using BuildingBlocks.Authentication.ServiceTokens;
using Liens.Infrastructure.Documents;
using Microsoft.Extensions.Logging.Abstractions;
using System.Net.Http.Json;

namespace Liens.Api.Tests.Tests;

public sealed class PayoffQuoteServiceTests
{
    [Fact]
    public async Task WaitForDocumentCleanAsync_retries_pending_scan_until_clean()
    {
        var handler = new ScanStatusHandler("PENDING", "CLEAN");
        var service = CreateService(handler);

        var result = await service.WaitForDocumentCleanAsync(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            CancellationToken.None);

        result.Should().BeTrue();
        handler.RequestCount.Should().Be(2);
    }

    [Theory]
    [InlineData("INFECTED")]
    [InlineData("FAILED")]
    [InlineData("SKIPPED")]
    public async Task WaitForDocumentCleanAsync_rejects_terminal_scan_status(string scanStatus)
    {
        var handler = new ScanStatusHandler(scanStatus);
        var service = CreateService(handler);

        var result = await service.WaitForDocumentCleanAsync(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            CancellationToken.None);

        result.Should().BeFalse();
        handler.RequestCount.Should().Be(1);
    }

    [Fact]
    public async Task WaitForDocumentCleanAsync_does_not_retry_non_transient_documents_error()
    {
        var handler = new StatusCodeHandler(System.Net.HttpStatusCode.NotFound);
        var service = CreateService(handler);

        var result = await service.WaitForDocumentCleanAsync(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            CancellationToken.None);

        result.Should().BeFalse();
        handler.RequestCount.Should().Be(1);
    }

    private static PayoffQuoteService CreateService(HttpMessageHandler handler)
    {
        var client = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://documents.test"),
        };

        return new PayoffQuoteService(
            null!,
            null!,
            null!,
            null!,
            null!,
            new SingleClientFactory(client),
            new DisabledServiceTokenIssuer(),
            NullLogger<PayoffQuoteService>.Instance);
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

    private sealed class ScanStatusHandler : HttpMessageHandler
    {
        private readonly Queue<string> _scanStatuses;
        private readonly string _lastScanStatus;

        public ScanStatusHandler(params string[] scanStatuses)
        {
            _scanStatuses = new Queue<string>(scanStatuses);
            _lastScanStatus = scanStatuses.Last();
        }

        public int RequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestCount++;
            var scanStatus = _scanStatuses.Count > 0
                ? _scanStatuses.Dequeue()
                : _lastScanStatus;

            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new
                {
                    data = new
                    {
                        id = Guid.NewGuid(),
                        scanStatus,
                    },
                }),
            });
        }
    }

    private sealed class StatusCodeHandler(System.Net.HttpStatusCode statusCode) : HttpMessageHandler
    {
        public int RequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestCount++;
            return Task.FromResult(new HttpResponseMessage(statusCode));
        }
    }
}
