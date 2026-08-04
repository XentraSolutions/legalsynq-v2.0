using System.Net;
using System.Net.Http.Headers;
using System.Text.Json.Nodes;
using Liens.Api.Tests.Helpers;
using Liens.Domain.Entities;
using Liens.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace Liens.Api.Tests.Tests;

public class LegacyDocumentLinkEndpointTests : IClassFixture<LiensApiFactory>, IAsyncLifetime
{
    private readonly LiensApiFactory _factory;
    private HttpClient _client = null!;

    public LegacyDocumentLinkEndpointTests(LiensApiFactory factory) => _factory = factory;

    public async Task InitializeAsync()
    {
        using var scope = _factory.Services.CreateScope();
        await SeedHelper.SeedAsync(scope.ServiceProvider);
        _client = _factory.CreateClient();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer", JwtTokenHelper.CreateFullAccessToken(SeedHelper.TenantId, SeedHelper.UserId));
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Resolve_returns_the_tenant_scoped_legacy_document_url()
    {
        const string objectKey = "gdVUbE5uDkuxszBhjoosIOOjHA4qoiVD.pdf";
        const string legacyUrl = "https://legal-dmm-prod.legalsynq.com/folder/gdVUbE5uDkuxszBhjoosIOOjHA4qoiVD.pdf";

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<LiensDbContext>();
            db.ServicingItems.Add(ServicingItem.Create(
                SeedHelper.TenantId,
                SeedHelper.OrgId,
                "LEGACY-DOC-RESOLVE-1",
                "LegacyCaseDocument",
                "Migrated legacy document",
                "Legacy migration",
                SeedHelper.UserId,
                caseId: SeedHelper.CaseId,
                notes: $"documentUrl={legacyUrl}; url={legacyUrl}; filename=legacy.pdf"));
            await db.SaveChangesAsync();
        }

        var response = await _client.GetAsync($"/api/liens/legacy-document-links/{objectKey}/resolve");

        response.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Body: {await response.Content.ReadAsStringAsync()}");
        var body = JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
        body["url"]!.GetValue<string>().Should().Be(legacyUrl);
    }
}
