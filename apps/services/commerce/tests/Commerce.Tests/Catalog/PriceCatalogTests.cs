using Commerce.Application.Common.Exceptions;
using Commerce.Contracts.Catalog;
using Commerce.Domain.Catalog.Enums;
using FluentAssertions;
using FluentValidation;
using Xunit;

namespace Commerce.Tests.Catalog;

public class PriceCatalogTests
{
    [Fact]
    public async Task Price_must_reference_exactly_one_item()
    {
        using var h = new CatalogTestHost();
        var plan = await h.PlanService.CreateAsync(
            new CreatePlanRequest(null, "plan", "P", null, BillingInterval.Monthly, null, 0), default);
        Func<Task> bothNull = () => h.PriceService.CreateAsync(
            new CreatePriceRequest(null, null, null, "USD", 100, BillingInterval.Monthly, h.Clock.UtcNow, null), default);
        await bothNull.Should().ThrowAsync<ValidationException>();

        var addon = await h.AddonService.CreateAsync(new CreateAddonRequest(null, "ad", "Ad", null), default);
        Func<Task> twoSet = () => h.PriceService.CreateAsync(
            new CreatePriceRequest(plan.Id, addon.Id, null, "USD", 100, BillingInterval.Monthly, h.Clock.UtcNow, null), default);
        await twoSet.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task Invalid_currency_rejected()
    {
        using var h = new CatalogTestHost();
        var plan = await h.PlanService.CreateAsync(
            new CreatePlanRequest(null, "plan", "P", null, BillingInterval.Monthly, null, 0), default);
        Func<Task> act = () => h.PriceService.CreateAsync(
            new CreatePriceRequest(plan.Id, null, null, "usd", 100, BillingInterval.Monthly, h.Clock.UtcNow, null), default);
        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task Negative_amount_rejected()
    {
        using var h = new CatalogTestHost();
        var plan = await h.PlanService.CreateAsync(
            new CreatePlanRequest(null, "plan", "P", null, BillingInterval.Monthly, null, 0), default);
        Func<Task> act = () => h.PriceService.CreateAsync(
            new CreatePriceRequest(plan.Id, null, null, "USD", -1, BillingInterval.Monthly, h.Clock.UtcNow, null), default);
        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task Effective_to_must_be_after_effective_from()
    {
        using var h = new CatalogTestHost();
        var plan = await h.PlanService.CreateAsync(
            new CreatePlanRequest(null, "plan", "P", null, BillingInterval.Monthly, null, 0), default);
        var from = h.Clock.UtcNow;
        Func<Task> act = () => h.PriceService.CreateAsync(
            new CreatePriceRequest(plan.Id, null, null, "USD", 100, BillingInterval.Monthly, from, from), default);
        await act.Should().ThrowAsync<ValidationException>();
    }

    [Fact]
    public async Task Overlapping_active_prices_rejected_on_activation()
    {
        using var h = new CatalogTestHost();
        var plan = await h.PlanService.CreateAsync(
            new CreatePlanRequest(null, "plan", "P", null, BillingInterval.Monthly, null, 0), default);

        var p1 = await h.PriceService.CreateAsync(
            new CreatePriceRequest(plan.Id, null, null, "USD", 100, BillingInterval.Monthly,
                new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc), null), default);
        await h.PriceService.ActivateAsync(p1.Id, default);

        var p2 = await h.PriceService.CreateAsync(
            new CreatePriceRequest(plan.Id, null, null, "USD", 200, BillingInterval.Monthly,
                new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc), null), default);

        Func<Task> act = () => h.PriceService.ActivateAsync(p2.Id, default);
        await act.Should().ThrowAsync<InvalidRelationshipException>();
    }

    [Fact]
    public async Task Updating_active_price_into_overlap_is_rejected()
    {
        using var h = new CatalogTestHost();
        var plan = await h.PlanService.CreateAsync(
            new CreatePlanRequest(null, "plan", "P", null, BillingInterval.Monthly, null, 0), default);

        var p1 = await h.PriceService.CreateAsync(
            new CreatePriceRequest(plan.Id, null, null, "USD", 100, BillingInterval.Monthly,
                new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc)), default);
        await h.PriceService.ActivateAsync(p1.Id, default);

        var p2 = await h.PriceService.CreateAsync(
            new CreatePriceRequest(plan.Id, null, null, "USD", 200, BillingInterval.Monthly,
                new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc), null), default);
        await h.PriceService.ActivateAsync(p2.Id, default);

        // Now try to update p2 to overlap p1 — must be rejected.
        Func<Task> act = () => h.PriceService.UpdateAsync(p2.Id,
            new UpdatePriceRequest("USD", 200, BillingInterval.Monthly,
                new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc), null), default);
        await act.Should().ThrowAsync<InvalidRelationshipException>();
    }

    [Fact]
    public async Task Non_overlapping_active_prices_allowed()
    {
        using var h = new CatalogTestHost();
        var plan = await h.PlanService.CreateAsync(
            new CreatePlanRequest(null, "plan", "P", null, BillingInterval.Monthly, null, 0), default);

        var p1 = await h.PriceService.CreateAsync(
            new CreatePriceRequest(plan.Id, null, null, "USD", 100, BillingInterval.Monthly,
                new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc)), default);
        await h.PriceService.ActivateAsync(p1.Id, default);

        var p2 = await h.PriceService.CreateAsync(
            new CreatePriceRequest(plan.Id, null, null, "USD", 200, BillingInterval.Monthly,
                new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc), null), default);
        var activated = await h.PriceService.ActivateAsync(p2.Id, default);
        activated.Status.Should().Be(CatalogStatus.Active);
    }
}
