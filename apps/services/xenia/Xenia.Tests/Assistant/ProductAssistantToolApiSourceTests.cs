using System.Net.Http.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xenia.Application.Assistant;
using Xenia.Infrastructure.Assistant;
using Xunit;

namespace Xenia.Tests.Assistant;

public sealed class ProductAssistantToolApiSourceTests
{
    [Fact]
    public void BuildAssistantToolPath_NormalizesRelativeSegments()
    {
        var path = TestProductAssistantToolApiSource.BuildPath("/providers/search");

        Assert.Equal("/api/assistant-tools/providers/search", path);
    }

    [Fact]
    public void EnsureAssistantToolPath_RejectsNonAssistantToolRoutes()
    {
        var ex = Assert.Throws<InvalidOperationException>(
            () => TestProductAssistantToolApiSource.EnsurePath("/api/providers"));

        Assert.Contains("/api/assistant-tools/", ex.Message);
    }

    [Fact]
    public void ProductAssistantSources_MustDeriveFromSharedAssistantToolBase()
    {
        var sourceTypes = typeof(ProductAssistantToolApiSource).Assembly.GetTypes()
            .Where(type =>
                type.IsClass &&
                !type.IsAbstract &&
                type.Namespace == "Xenia.Infrastructure.Assistant" &&
                type.Name.EndsWith("AssistantSource", StringComparison.Ordinal))
            .ToList();

        Assert.NotEmpty(sourceTypes);
        Assert.All(sourceTypes, type => Assert.True(
            typeof(ProductAssistantToolApiSource).IsAssignableFrom(type),
            $"{type.FullName} must derive from {nameof(ProductAssistantToolApiSource)}."));
    }

    [Fact]
    public async Task CareConnectAssistantSource_UsesAssistantToolApiSurface()
    {
        var handler = new RecordingHandler();
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://careconnect.local")
        };

        var contextAccessor = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext()
        };
        contextAccessor.HttpContext.Request.Headers.Authorization = "Bearer test-token";

        var options = Options.Create(new XeniaAssistantOptions
        {
            CareConnect = new XeniaAssistantOptions.CareConnectOptions
            {
                BaseUrl = "http://careconnect.local",
                TimeoutSeconds = 15,
                MaxHistoryItems = 5,
            }
        });

        var source = new CareConnectAssistantSource(
            httpClient,
            contextAccessor,
            options,
            NullLogger<CareConnectAssistantSource>.Instance);

        await source.LookupReferralAsync(Guid.Parse("11111111-1111-1111-1111-111111111111"));

        Assert.NotNull(handler.LastRequestUri);
        Assert.StartsWith("/api/assistant-tools/", handler.LastRequestUri!.AbsolutePath, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SynqLienAssistantSource_UsesAssistantToolApiSurface()
    {
        var handler = new RecordingHandler();
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://synqlien.local")
        };

        var contextAccessor = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext()
        };
        contextAccessor.HttpContext.Request.Headers.Authorization = "Bearer test-token";

        var source = new SynqLienAssistantSource(
            httpClient,
            contextAccessor,
            NullLogger<SynqLienAssistantSource>.Instance);

        await source.LookupLienAsync(new SynqLienLienLookupRequest(
            Guid.Parse("22222222-2222-2222-2222-222222222222"),
            null));

        Assert.NotNull(handler.LastRequestUri);
        Assert.StartsWith("/api/assistant-tools/", handler.LastRequestUri!.AbsolutePath, StringComparison.Ordinal);
    }

    private sealed class TestProductAssistantToolApiSource : ProductAssistantToolApiSource
    {
        public static string BuildPath(string relativePath) => BuildAssistantToolPath(relativePath);

        public static void EnsurePath(string path) => EnsureAssistantToolPath(path);
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public Uri? LastRequestUri { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequestUri = request.RequestUri;

            var response = new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new
                {
                    succeeded = true,
                    status = "completed",
                    safeError = (string?)null,
                    referral = new
                    {
                        referralId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                        status = "New",
                        urgency = "Urgent",
                        providerName = "Atlas Medical",
                        clientDisplayName = "Jane Doe",
                        requestedService = "Physical Therapy",
                        treatmentTypeName = "Orthopedics",
                        caseNumber = "CASE-1",
                        referringOrganizationName = "Acme Law",
                        referrerName = "Pat Referrer",
                        createdAtUtc = DateTime.UtcNow.AddDays(-2),
                        updatedAtUtc = DateTime.UtcNow,
                        history = Array.Empty<object>(),
                    }
                })
            };

            return Task.FromResult(response);
        }
    }
}
