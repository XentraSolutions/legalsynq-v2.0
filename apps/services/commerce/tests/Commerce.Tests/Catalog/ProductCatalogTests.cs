using Commerce.Application.Common.Exceptions;
using Commerce.Contracts.Catalog;
using Commerce.Domain.Catalog.Enums;
using FluentAssertions;
using Xunit;

namespace Commerce.Tests.Catalog;

public class ProductCatalogTests
{
    [Fact]
    public async Task Create_product_succeeds_and_normalizes_key()
    {
        using var h = new CatalogTestHost();
        var p = await h.ProductService.CreateAsync(new CreateProductRequest("My-Product", "My Product", "Desc"), default);
        p.Key.Should().Be("my-product");
        p.Status.Should().Be(CatalogStatus.Draft);
    }

    [Fact]
    public async Task Duplicate_product_key_is_rejected()
    {
        using var h = new CatalogTestHost();
        await h.ProductService.CreateAsync(new CreateProductRequest("dup", "Dup", null), default);
        Func<Task> act = () => h.ProductService.CreateAsync(new CreateProductRequest("DUP", "Dup2", null), default);
        await act.Should().ThrowAsync<DuplicateKeyException>();
    }

    [Fact]
    public async Task Activate_then_retire_product()
    {
        using var h = new CatalogTestHost();
        var p = await h.ProductService.CreateAsync(new CreateProductRequest("p1", "P1", null), default);
        var active = await h.ProductService.ActivateAsync(p.Id, default);
        active.Status.Should().Be(CatalogStatus.Active);
        var retired = await h.ProductService.RetireAsync(p.Id, default);
        retired.Status.Should().Be(CatalogStatus.Retired);
    }

    [Fact]
    public async Task Activating_a_retired_product_fails()
    {
        using var h = new CatalogTestHost();
        var p = await h.ProductService.CreateAsync(new CreateProductRequest("p2", "P2", null), default);
        await h.ProductService.RetireAsync(p.Id, default);
        Func<Task> act = () => h.ProductService.ActivateAsync(p.Id, default);
        await act.Should().ThrowAsync<InvalidStateTransitionException>();
    }
}
