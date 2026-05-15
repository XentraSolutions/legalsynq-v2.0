using System.Net;
using System.Net.Http.Json;
using Commerce.Contracts.Catalog;
using Commerce.Domain.Catalog.Enums;
using FluentAssertions;
using Xunit;

namespace Commerce.Tests.Catalog;

public class CatalogApiTests : IClassFixture<CommerceWebApplicationFactory>
{
    private readonly CommerceWebApplicationFactory _factory;
    public CatalogApiTests(CommerceWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task Health_and_swagger_still_load()
    {
        var client = _factory.CreateClient();
        (await client.GetAsync("/health")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.GetAsync("/swagger/v1/swagger.json")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Product_create_get_list_roundtrip()
    {
        var client = _factory.CreateClient();
        var key = "api-prod-" + Guid.NewGuid().ToString("N")[..8];

        var create = await client.PostAsJsonAsync("/api/commerce/catalog/products",
            new CreateProductRequest(key, "Api Prod", "via api"));
        create.StatusCode.Should().Be(HttpStatusCode.Created);

        var created = await create.Content.ReadFromJsonAsync<ProductResponse>();
        created!.Status.Should().Be(CatalogStatus.Draft);

        var get = await client.GetAsync($"/api/commerce/catalog/products/{created.Id}");
        get.StatusCode.Should().Be(HttpStatusCode.OK);

        var list = await client.GetAsync("/api/commerce/catalog/products");
        list.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Duplicate_product_key_returns_409()
    {
        var client = _factory.CreateClient();
        var key = "dup-" + Guid.NewGuid().ToString("N")[..8];
        await client.PostAsJsonAsync("/api/commerce/catalog/products", new CreateProductRequest(key, "A", null));
        var second = await client.PostAsJsonAsync("/api/commerce/catalog/products", new CreateProductRequest(key, "B", null));
        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Get_unknown_product_returns_404()
    {
        var client = _factory.CreateClient();
        var resp = await client.GetAsync($"/api/commerce/catalog/products/{Guid.NewGuid()}");
        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Validation_failure_returns_400()
    {
        var client = _factory.CreateClient();
        var resp = await client.PostAsJsonAsync("/api/commerce/catalog/products",
            new CreateProductRequest("", "", null));
        resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
