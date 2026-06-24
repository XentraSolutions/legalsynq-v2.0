using Commerce.Application.Common.Exceptions;
using Commerce.Contracts.Catalog;
using Commerce.Domain.Catalog.Enums;
using FluentAssertions;
using Xunit;

namespace Commerce.Tests.Catalog;

public class FeatureCatalogTests
{
    [Fact]
    public async Task Create_feature_under_product_succeeds()
    {
        using var h = new CatalogTestHost();
        var p = await h.ProductService.CreateAsync(new CreateProductRequest("prod-1", "Prod 1", null), default);
        var f = await h.FeatureService.CreateAsync(p.Id, new CreateFeatureRequest("f1", "F1", null, FeatureType.Boolean), default);
        f.ProductId.Should().Be(p.Id);
        f.Key.Should().Be("f1");
        f.FeatureType.Should().Be(FeatureType.Boolean);
    }

    [Fact]
    public async Task Duplicate_feature_key_within_product_rejected()
    {
        using var h = new CatalogTestHost();
        var p = await h.ProductService.CreateAsync(new CreateProductRequest("prod-2", "Prod 2", null), default);
        await h.FeatureService.CreateAsync(p.Id, new CreateFeatureRequest("f1", "F1", null, FeatureType.Boolean), default);
        Func<Task> act = () => h.FeatureService.CreateAsync(p.Id, new CreateFeatureRequest("F1", "Other", null, FeatureType.Limit), default);
        await act.Should().ThrowAsync<DuplicateKeyException>();
    }

    [Fact]
    public async Task Same_feature_key_allowed_under_different_products()
    {
        using var h = new CatalogTestHost();
        var p1 = await h.ProductService.CreateAsync(new CreateProductRequest("prod-a", "A", null), default);
        var p2 = await h.ProductService.CreateAsync(new CreateProductRequest("prod-b", "B", null), default);
        await h.FeatureService.CreateAsync(p1.Id, new CreateFeatureRequest("shared", "Shared A", null, FeatureType.Boolean), default);
        var f2 = await h.FeatureService.CreateAsync(p2.Id, new CreateFeatureRequest("shared", "Shared B", null, FeatureType.Boolean), default);
        f2.Should().NotBeNull();
    }

    [Fact]
    public async Task Cannot_add_feature_to_retired_product()
    {
        using var h = new CatalogTestHost();
        var p = await h.ProductService.CreateAsync(new CreateProductRequest("prod-x", "X", null), default);
        await h.ProductService.RetireAsync(p.Id, default);
        Func<Task> act = () => h.FeatureService.CreateAsync(p.Id, new CreateFeatureRequest("f1", "F1", null, FeatureType.Boolean), default);
        await act.Should().ThrowAsync<InvalidRelationshipException>();
    }
}
