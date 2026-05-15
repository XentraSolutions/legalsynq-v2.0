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

public sealed class PriceCatalogService : IPriceCatalogService
{
    private readonly CommerceDbContext _db;
    private readonly IClock _clock;
    private readonly IValidator<CreatePriceRequest> _createValidator;
    private readonly IValidator<UpdatePriceRequest> _updateValidator;

    public PriceCatalogService(
        CommerceDbContext db,
        IClock clock,
        IValidator<CreatePriceRequest> createValidator,
        IValidator<UpdatePriceRequest> updateValidator)
    {
        _db = db;
        _clock = clock;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
    }

    public async Task<PriceResponse> CreateAsync(CreatePriceRequest request, CancellationToken ct)
    {
        await _createValidator.ValidateAndThrowAsync(request, ct);

        await EnsureReferenceExists(request.PlanId, request.AddonId, request.BundleId, ct);

        Price price;
        try
        {
            price = Price.Create(request.PlanId, request.AddonId, request.BundleId,
                request.Currency, request.AmountMinor, request.BillingInterval,
                request.EffectiveFromUtc, request.EffectiveToUtc, _clock.UtcNow);
        }
        catch (InvalidOperationException ex)
        {
            throw new InvalidRelationshipException(ex.Message);
        }

        _db.Prices.Add(price);
        await _db.SaveChangesAsync(ct);
        return price.ToResponse();
    }

    public async Task<IReadOnlyList<PriceResponse>> ListAsync(CancellationToken ct)
    {
        var items = await _db.Prices.AsNoTracking()
            .OrderBy(p => p.Currency).ThenBy(p => p.EffectiveFromUtc)
            .ToListAsync(ct);
        return items.Select(CatalogMappers.ToResponse).ToList();
    }

    public async Task<PriceResponse> GetAsync(Guid id, CancellationToken ct)
    {
        var p = await _db.Prices.FindAsync(new object[] { id }, ct)
                ?? throw new NotFoundException("Price", id.ToString());
        return p.ToResponse();
    }

    public async Task<PriceResponse> UpdateAsync(Guid id, UpdatePriceRequest request, CancellationToken ct)
    {
        await _updateValidator.ValidateAndThrowAsync(request, ct);
        var p = await _db.Prices.FindAsync(new object[] { id }, ct)
                ?? throw new NotFoundException("Price", id.ToString());
        try
        {
            p.Update(request.Currency, request.AmountMinor, request.BillingInterval,
                request.EffectiveFromUtc, request.EffectiveToUtc, _clock.UtcNow);
        }
        catch (InvalidOperationException ex)
        {
            throw new InvalidRelationshipException(ex.Message);
        }

        // If the price is currently Active, the overlap invariant must continue
        // to hold after the mutation. Re-check against other Active prices.
        if (p.Status == CatalogStatus.Active)
            await EnsureNoActiveOverlap(p, ct);

        await _db.SaveChangesAsync(ct);
        return p.ToResponse();
    }

    public async Task<PriceResponse> ActivateAsync(Guid id, CancellationToken ct)
    {
        var p = await _db.Prices.FindAsync(new object[] { id }, ct)
                ?? throw new NotFoundException("Price", id.ToString());

        await EnsureNoActiveOverlap(p, ct);

        try { p.Activate(_clock.UtcNow); }
        catch (InvalidOperationException ex) { throw new InvalidStateTransitionException(ex.Message); }
        await _db.SaveChangesAsync(ct);
        return p.ToResponse();
    }

    private async Task EnsureNoActiveOverlap(Domain.Catalog.Price p, CancellationToken ct)
    {
        var actives = await _db.Prices.AsNoTracking()
            .Where(x => x.Id != p.Id
                     && x.Status == CatalogStatus.Active
                     && x.Currency == p.Currency
                     && x.BillingInterval == p.BillingInterval
                     && x.PlanId == p.PlanId
                     && x.AddonId == p.AddonId
                     && x.BundleId == p.BundleId)
            .ToListAsync(ct);

        foreach (var other in actives)
        {
            if (p.OverlapsWith(other))
                throw new InvalidRelationshipException(
                    "Active prices must not overlap for the same item, currency, and billing interval.");
        }
    }

    public async Task<PriceResponse> RetireAsync(Guid id, CancellationToken ct)
    {
        var p = await _db.Prices.FindAsync(new object[] { id }, ct)
                ?? throw new NotFoundException("Price", id.ToString());
        p.Retire(_clock.UtcNow);
        await _db.SaveChangesAsync(ct);
        return p.ToResponse();
    }

    private async Task EnsureReferenceExists(Guid? planId, Guid? addonId, Guid? bundleId, CancellationToken ct)
    {
        if (planId.HasValue && !await _db.Plans.AnyAsync(x => x.Id == planId.Value, ct))
            throw new NotFoundException("Plan", planId.Value.ToString());
        if (addonId.HasValue && !await _db.Addons.AnyAsync(x => x.Id == addonId.Value, ct))
            throw new NotFoundException("Addon", addonId.Value.ToString());
        if (bundleId.HasValue && !await _db.Bundles.AnyAsync(x => x.Id == bundleId.Value, ct))
            throw new NotFoundException("Bundle", bundleId.Value.ToString());
    }
}
