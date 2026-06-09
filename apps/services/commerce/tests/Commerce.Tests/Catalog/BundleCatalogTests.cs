using Commerce.Application.Common.Exceptions;
using Commerce.Contracts.Catalog;
using Commerce.Domain.Catalog.Enums;
using FluentAssertions;
using FluentValidation;
using Xunit;

namespace Commerce.Tests.Catalog;

public class BundleCatalogTests
{
    [Fact]
    public async Task Bundle_item_must_reference_exactly_one()
    {
        using var h = new CatalogTestHost();
        var b = await h.BundleService.CreateAsync(new CreateBundleRequest("bun", "Bundle", null), default);
        Func<Task> none = () => h.BundleService.AddItemAsync(b.Id, new AddBundleItemRequest(null, null, null), default);
        await none.Should().ThrowAsync<ValidationException>();

        var p = await h.ProductService.CreateAsync(new CreateProductRequest("prod", "P", null), default);
        var pl = await h.PlanService.CreateAsync(
            new CreatePlanRequest(p.Id, "pl1", "Pl", null, BillingInterval.Monthly, null, 0), default);
        Func<Task> two = () => h.BundleService.AddItemAsync(b.Id, new AddBundleItemRequest(p.Id, pl.Id, null), default);
        await two.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task Bundle_cannot_include_retired_item()
    {
        using var h = new CatalogTestHost();
        var b = await h.BundleService.CreateAsync(new CreateBundleRequest("bun", "Bundle", null), default);
        var p = await h.ProductService.CreateAsync(new CreateProductRequest("prod", "P", null), default);
        await h.ProductService.RetireAsync(p.Id, default);
        Func<Task> act = () => h.BundleService.AddItemAsync(b.Id, new AddBundleItemRequest(p.Id, null, null), default);
        await act.Should().ThrowAsync<InvalidRelationshipException>();
    }

    [Fact]
    public async Task Bundle_item_added_successfully_and_listed()
    {
        using var h = new CatalogTestHost();
        var b = await h.BundleService.CreateAsync(new CreateBundleRequest("bun", "Bundle", null), default);
        var p = await h.ProductService.CreateAsync(new CreateProductRequest("prod", "P", null), default);
        var item = await h.BundleService.AddItemAsync(b.Id, new AddBundleItemRequest(p.Id, null, null), default);
        item.ProductId.Should().Be(p.Id);

        var listed = await h.BundleService.ListItemsAsync(b.Id, default);
        listed.Should().HaveCount(1);
    }
}
