using Commerce.Application.Common.Exceptions;
using Commerce.Contracts.Catalog;
using Commerce.Domain.Catalog.Enums;
using FluentAssertions;
using Xunit;

namespace Commerce.Tests.Catalog;

public class PlanCatalogTests
{
    [Fact]
    public async Task Create_plan_for_product_succeeds()
    {
        using var h = new CatalogTestHost();
        var p = await h.ProductService.CreateAsync(new CreateProductRequest("prod", "P", null), default);
        var plan = await h.PlanService.CreateAsync(
            new CreatePlanRequest(p.Id, "basic", "Basic", null, BillingInterval.Monthly, null, 0), default);
        plan.ProductId.Should().Be(p.Id);
        plan.Status.Should().Be(CatalogStatus.Draft);
    }

    [Fact]
    public async Task Activate_plan_succeeds()
    {
        using var h = new CatalogTestHost();
        var p = await h.ProductService.CreateAsync(new CreateProductRequest("prod", "P", null), default);
        var plan = await h.PlanService.CreateAsync(
            new CreatePlanRequest(p.Id, "basic", "Basic", null, BillingInterval.Monthly, null, 0), default);
        var active = await h.PlanService.ActivateAsync(plan.Id, default);
        active.Status.Should().Be(CatalogStatus.Active);
    }

    [Fact]
    public async Task Plan_cannot_reference_retired_product()
    {
        using var h = new CatalogTestHost();
        var p = await h.ProductService.CreateAsync(new CreateProductRequest("prod", "P", null), default);
        await h.ProductService.RetireAsync(p.Id, default);
        Func<Task> act = () => h.PlanService.CreateAsync(
            new CreatePlanRequest(p.Id, "basic", "Basic", null, BillingInterval.Monthly, null, 0), default);
        await act.Should().ThrowAsync<InvalidRelationshipException>();
    }

    [Fact]
    public async Task Retired_plan_only_allows_metadata_updates()
    {
        using var h = new CatalogTestHost();
        var plan = await h.PlanService.CreateAsync(
            new CreatePlanRequest(null, "global", "G", null, BillingInterval.Monthly, 14, 0), default);
        await h.PlanService.RetireAsync(plan.Id, default);
        var updated = await h.PlanService.UpdateAsync(plan.Id,
            new UpdatePlanRequest("New Name", "Note", BillingInterval.Annual, 30, 99), default);
        updated.Name.Should().Be("New Name");
        updated.Description.Should().Be("Note");
        // BillingInterval and TrialDays must NOT change while retired.
        updated.BillingInterval.Should().Be(BillingInterval.Monthly);
        updated.TrialDays.Should().Be(14);
        updated.SortOrder.Should().Be(0);
    }
}

public class PlanFeatureTests
{
    [Fact]
    public async Task Limit_feature_requires_LimitValue_when_enabled()
    {
        using var h = new CatalogTestHost();
        var p = await h.ProductService.CreateAsync(new CreateProductRequest("prod", "P", null), default);
        var f = await h.FeatureService.CreateAsync(p.Id, new CreateFeatureRequest("seats", "Seats", null, FeatureType.Limit), default);
        var plan = await h.PlanService.CreateAsync(
            new CreatePlanRequest(p.Id, "basic", "Basic", null, BillingInterval.Monthly, null, 0), default);
        Func<Task> act = () => h.PlanService.AddFeatureAsync(plan.Id,
            new AddPlanFeatureRequest(f.Id, IsEnabled: true, LimitValue: null, MeteredIncludedUnits: null), default);
        await act.Should().ThrowAsync<InvalidRelationshipException>();
    }

    [Fact]
    public async Task Boolean_feature_does_not_require_LimitValue()
    {
        using var h = new CatalogTestHost();
        var p = await h.ProductService.CreateAsync(new CreateProductRequest("prod", "P", null), default);
        var f = await h.FeatureService.CreateAsync(p.Id, new CreateFeatureRequest("flag", "Flag", null, FeatureType.Boolean), default);
        var plan = await h.PlanService.CreateAsync(
            new CreatePlanRequest(p.Id, "basic", "Basic", null, BillingInterval.Monthly, null, 0), default);
        var pf = await h.PlanService.AddFeatureAsync(plan.Id,
            new AddPlanFeatureRequest(f.Id, IsEnabled: true, LimitValue: null, MeteredIncludedUnits: null), default);
        pf.IsEnabled.Should().BeTrue();
        pf.LimitValue.Should().BeNull();
    }

    [Fact]
    public async Task Boolean_feature_with_LimitValue_rejected()
    {
        using var h = new CatalogTestHost();
        var p = await h.ProductService.CreateAsync(new CreateProductRequest("prod", "P", null), default);
        var f = await h.FeatureService.CreateAsync(p.Id, new CreateFeatureRequest("flag", "Flag", null, FeatureType.Boolean), default);
        var plan = await h.PlanService.CreateAsync(
            new CreatePlanRequest(p.Id, "basic", "Basic", null, BillingInterval.Monthly, null, 0), default);
        Func<Task> act = () => h.PlanService.AddFeatureAsync(plan.Id,
            new AddPlanFeatureRequest(f.Id, IsEnabled: true, LimitValue: 10, MeteredIncludedUnits: null), default);
        await act.Should().ThrowAsync<InvalidRelationshipException>();
    }

    [Fact]
    public async Task Plan_feature_product_mismatch_rejected()
    {
        using var h = new CatalogTestHost();
        var p1 = await h.ProductService.CreateAsync(new CreateProductRequest("prod1", "P1", null), default);
        var p2 = await h.ProductService.CreateAsync(new CreateProductRequest("prod2", "P2", null), default);
        var f = await h.FeatureService.CreateAsync(p1.Id, new CreateFeatureRequest("feat", "F", null, FeatureType.Boolean), default);
        var plan = await h.PlanService.CreateAsync(
            new CreatePlanRequest(p2.Id, "basic", "Basic", null, BillingInterval.Monthly, null, 0), default);
        Func<Task> act = () => h.PlanService.AddFeatureAsync(plan.Id,
            new AddPlanFeatureRequest(f.Id, true, null, null), default);
        await act.Should().ThrowAsync<InvalidRelationshipException>();
    }

    [Fact]
    public async Task Retired_plan_rejects_AddFeature()
    {
        using var h = new CatalogTestHost();
        var p = await h.ProductService.CreateAsync(new CreateProductRequest("prod", "P", null), default);
        var f = await h.FeatureService.CreateAsync(p.Id, new CreateFeatureRequest("flag", "Flag", null, FeatureType.Boolean), default);
        var plan = await h.PlanService.CreateAsync(
            new CreatePlanRequest(p.Id, "basic", "Basic", null, BillingInterval.Monthly, null, 0), default);
        await h.PlanService.RetireAsync(plan.Id, default);
        Func<Task> act = () => h.PlanService.AddFeatureAsync(plan.Id,
            new AddPlanFeatureRequest(f.Id, true, null, null), default);
        await act.Should().ThrowAsync<InvalidStateTransitionException>();
    }

    [Fact]
    public async Task Retired_plan_rejects_RemoveFeature()
    {
        using var h = new CatalogTestHost();
        var p = await h.ProductService.CreateAsync(new CreateProductRequest("prod", "P", null), default);
        var f = await h.FeatureService.CreateAsync(p.Id, new CreateFeatureRequest("flag", "Flag", null, FeatureType.Boolean), default);
        var plan = await h.PlanService.CreateAsync(
            new CreatePlanRequest(p.Id, "basic", "Basic", null, BillingInterval.Monthly, null, 0), default);
        await h.PlanService.AddFeatureAsync(plan.Id, new AddPlanFeatureRequest(f.Id, true, null, null), default);
        await h.PlanService.RetireAsync(plan.Id, default);
        Func<Task> act = () => h.PlanService.RemoveFeatureAsync(plan.Id, f.Id, default);
        await act.Should().ThrowAsync<InvalidStateTransitionException>();
    }

    [Fact]
    public async Task Retired_feature_cannot_be_added_to_plan()
    {
        using var h = new CatalogTestHost();
        var p = await h.ProductService.CreateAsync(new CreateProductRequest("prod", "P", null), default);
        var f = await h.FeatureService.CreateAsync(p.Id, new CreateFeatureRequest("feat", "F", null, FeatureType.Boolean), default);
        await h.FeatureService.RetireAsync(f.Id, default);
        var plan = await h.PlanService.CreateAsync(
            new CreatePlanRequest(p.Id, "basic", "Basic", null, BillingInterval.Monthly, null, 0), default);
        Func<Task> act = () => h.PlanService.AddFeatureAsync(plan.Id,
            new AddPlanFeatureRequest(f.Id, true, null, null), default);
        await act.Should().ThrowAsync<InvalidRelationshipException>();
    }
}
