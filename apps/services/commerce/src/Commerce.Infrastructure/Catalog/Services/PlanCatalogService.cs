using Commerce.Application.Catalog.Abstractions;
using Commerce.Application.Common.Exceptions;
using Commerce.Application.Common.Time;
using Commerce.Contracts.Catalog;
using Commerce.Domain.Catalog;
using Commerce.Domain.Catalog.Enums;
using Commerce.Infrastructure.Catalog.Mapping;
using Commerce.Infrastructure.Persistence;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Commerce.Infrastructure.Catalog.Services;

public sealed class PlanCatalogService : IPlanCatalogService
{
    private readonly CommerceDbContext _db;
    private readonly IClock _clock;
    private readonly IValidator<CreatePlanRequest> _createValidator;
    private readonly IValidator<UpdatePlanRequest> _updateValidator;
    private readonly IValidator<AddPlanFeatureRequest> _addFeatureValidator;

    public PlanCatalogService(
        CommerceDbContext db,
        IClock clock,
        IValidator<CreatePlanRequest> createValidator,
        IValidator<UpdatePlanRequest> updateValidator,
        IValidator<AddPlanFeatureRequest> addFeatureValidator)
    {
        _db = db;
        _clock = clock;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
        _addFeatureValidator = addFeatureValidator;
    }

    public async Task<PlanResponse> CreateAsync(CreatePlanRequest request, CancellationToken ct)
    {
        await _createValidator.ValidateAndThrowAsync(request, ct);

        if (request.ProductId.HasValue)
        {
            var product = await _db.Products.FindAsync(new object[] { request.ProductId.Value }, ct)
                          ?? throw new NotFoundException("Product", request.ProductId.Value.ToString());
            if (product.Status == CatalogStatus.Retired)
                throw new InvalidRelationshipException("Cannot create a plan for a retired product.");
        }

        var key = CatalogKey.Normalize(request.Key);
        if (await _db.Plans.AnyAsync(p => p.Key == key, ct))
            throw new DuplicateKeyException("Plan", key);

        var plan = Plan.Create(
            request.ProductId, request.Key, request.Name, request.Description,
            request.BillingInterval, request.TrialDays, request.SortOrder, _clock.UtcNow);
        _db.Plans.Add(plan);
        await _db.SaveChangesAsync(ct);
        return plan.ToResponse();
    }

    public async Task<IReadOnlyList<PlanResponse>> ListAsync(CancellationToken ct)
    {
        var items = await _db.Plans.AsNoTracking().OrderBy(p => p.SortOrder).ThenBy(p => p.Key).ToListAsync(ct);
        return items.Select(CatalogMappers.ToResponse).ToList();
    }

    public async Task<PlanResponse> GetAsync(Guid id, CancellationToken ct)
    {
        var p = await _db.Plans.FindAsync(new object[] { id }, ct)
                ?? throw new NotFoundException("Plan", id.ToString());
        return p.ToResponse();
    }

    public async Task<PlanResponse> UpdateAsync(Guid id, UpdatePlanRequest request, CancellationToken ct)
    {
        await _updateValidator.ValidateAndThrowAsync(request, ct);
        var p = await _db.Plans.FindAsync(new object[] { id }, ct)
                ?? throw new NotFoundException("Plan", id.ToString());
        p.Update(request.Name, request.Description, request.BillingInterval, request.TrialDays, request.SortOrder, _clock.UtcNow);
        await _db.SaveChangesAsync(ct);
        return p.ToResponse();
    }

    public async Task<PlanResponse> ActivateAsync(Guid id, CancellationToken ct)
    {
        var p = await _db.Plans.FindAsync(new object[] { id }, ct)
                ?? throw new NotFoundException("Plan", id.ToString());

        if (p.ProductId.HasValue)
        {
            var product = await _db.Products.FindAsync(new object[] { p.ProductId.Value }, ct);
            if (product is null || product.Status == CatalogStatus.Retired)
                throw new InvalidRelationshipException("Cannot activate a plan attached to a retired product.");
        }

        try { p.Activate(_clock.UtcNow); }
        catch (InvalidOperationException ex) { throw new InvalidStateTransitionException(ex.Message); }
        await _db.SaveChangesAsync(ct);
        return p.ToResponse();
    }

    public async Task<PlanResponse> RetireAsync(Guid id, CancellationToken ct)
    {
        var p = await _db.Plans.FindAsync(new object[] { id }, ct)
                ?? throw new NotFoundException("Plan", id.ToString());
        p.Retire(_clock.UtcNow);
        await _db.SaveChangesAsync(ct);
        return p.ToResponse();
    }

    public async Task<PlanFeatureResponse> AddFeatureAsync(Guid planId, AddPlanFeatureRequest request, CancellationToken ct)
    {
        await _addFeatureValidator.ValidateAndThrowAsync(request, ct);

        var plan = await _db.Plans.FindAsync(new object[] { planId }, ct)
                   ?? throw new NotFoundException("Plan", planId.ToString());

        if (plan.Status == CatalogStatus.Retired)
            throw new InvalidStateTransitionException("Cannot modify features on a retired plan.");

        var feature = await _db.Features.FindAsync(new object[] { request.FeatureId }, ct)
                      ?? throw new NotFoundException("Feature", request.FeatureId.ToString());

        if (feature.Status == CatalogStatus.Retired)
            throw new InvalidRelationshipException("Retired features cannot be added to plans.");

        if (plan.ProductId.HasValue && feature.ProductId != plan.ProductId.Value)
            throw new InvalidRelationshipException("Feature product must match plan product when plan is product-specific.");

        // Type-specific validation
        if (request.IsEnabled)
        {
            if (feature.FeatureType == FeatureType.Limit && !request.LimitValue.HasValue)
                throw new InvalidRelationshipException("Limit feature requires LimitValue when enabled.");
            if (feature.FeatureType == FeatureType.Boolean && request.LimitValue.HasValue)
                throw new InvalidRelationshipException("Boolean feature must not have LimitValue.");
        }

        if (await _db.PlanFeatures.AnyAsync(pf => pf.PlanId == planId && pf.FeatureId == request.FeatureId, ct))
            throw new DuplicateKeyException("PlanFeature", $"{planId}:{request.FeatureId}");

        var pf = PlanFeature.Create(planId, request.FeatureId, request.IsEnabled, request.LimitValue, request.MeteredIncludedUnits, _clock.UtcNow);
        _db.PlanFeatures.Add(pf);
        await _db.SaveChangesAsync(ct);
        return pf.ToResponse();
    }

    public async Task<IReadOnlyList<PlanFeatureResponse>> ListFeaturesAsync(Guid planId, CancellationToken ct)
    {
        if (!await _db.Plans.AnyAsync(p => p.Id == planId, ct))
            throw new NotFoundException("Plan", planId.ToString());
        var items = await _db.PlanFeatures.AsNoTracking().Where(pf => pf.PlanId == planId).ToListAsync(ct);
        return items.Select(CatalogMappers.ToResponse).ToList();
    }

    public async Task RemoveFeatureAsync(Guid planId, Guid featureId, CancellationToken ct)
    {
        var plan = await _db.Plans.FindAsync(new object[] { planId }, ct)
                   ?? throw new NotFoundException("Plan", planId.ToString());

        if (plan.Status == CatalogStatus.Retired)
            throw new InvalidStateTransitionException("Cannot modify features on a retired plan.");

        var pf = await _db.PlanFeatures.FirstOrDefaultAsync(x => x.PlanId == planId && x.FeatureId == featureId, ct)
                 ?? throw new NotFoundException("PlanFeature", $"{planId}:{featureId}");
        _db.PlanFeatures.Remove(pf);
        await _db.SaveChangesAsync(ct);
    }
}
