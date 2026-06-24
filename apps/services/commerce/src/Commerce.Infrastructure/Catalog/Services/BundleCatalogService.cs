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

public sealed class BundleCatalogService : IBundleCatalogService
{
    private readonly CommerceDbContext _db;
    private readonly IClock _clock;
    private readonly IValidator<CreateBundleRequest> _createValidator;
    private readonly IValidator<UpdateBundleRequest> _updateValidator;
    private readonly IValidator<AddBundleItemRequest> _addItemValidator;

    public BundleCatalogService(
        CommerceDbContext db,
        IClock clock,
        IValidator<CreateBundleRequest> createValidator,
        IValidator<UpdateBundleRequest> updateValidator,
        IValidator<AddBundleItemRequest> addItemValidator)
    {
        _db = db;
        _clock = clock;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
        _addItemValidator = addItemValidator;
    }

    public async Task<BundleResponse> CreateAsync(CreateBundleRequest request, CancellationToken ct)
    {
        await _createValidator.ValidateAndThrowAsync(request, ct);
        var key = CatalogKey.Normalize(request.Key);
        if (await _db.Bundles.AnyAsync(b => b.Key == key, ct))
            throw new DuplicateKeyException("Bundle", key);

        var bundle = Bundle.Create(request.Key, request.Name, request.Description, _clock.UtcNow);
        _db.Bundles.Add(bundle);
        await _db.SaveChangesAsync(ct);
        return bundle.ToResponse();
    }

    public async Task<IReadOnlyList<BundleResponse>> ListAsync(CancellationToken ct)
    {
        var items = await _db.Bundles.AsNoTracking().OrderBy(b => b.Key).ToListAsync(ct);
        return items.Select(CatalogMappers.ToResponse).ToList();
    }

    public async Task<BundleResponse> GetAsync(Guid id, CancellationToken ct)
    {
        var b = await _db.Bundles.FindAsync(new object[] { id }, ct)
                ?? throw new NotFoundException("Bundle", id.ToString());
        return b.ToResponse();
    }

    public async Task<BundleResponse> UpdateAsync(Guid id, UpdateBundleRequest request, CancellationToken ct)
    {
        await _updateValidator.ValidateAndThrowAsync(request, ct);
        var b = await _db.Bundles.FindAsync(new object[] { id }, ct)
                ?? throw new NotFoundException("Bundle", id.ToString());
        b.Update(request.Name, request.Description, _clock.UtcNow);
        await _db.SaveChangesAsync(ct);
        return b.ToResponse();
    }

    public async Task<BundleResponse> ActivateAsync(Guid id, CancellationToken ct)
    {
        var b = await _db.Bundles.FindAsync(new object[] { id }, ct)
                ?? throw new NotFoundException("Bundle", id.ToString());
        try { b.Activate(_clock.UtcNow); }
        catch (InvalidOperationException ex) { throw new InvalidStateTransitionException(ex.Message); }
        await _db.SaveChangesAsync(ct);
        return b.ToResponse();
    }

    public async Task<BundleResponse> RetireAsync(Guid id, CancellationToken ct)
    {
        var b = await _db.Bundles.FindAsync(new object[] { id }, ct)
                ?? throw new NotFoundException("Bundle", id.ToString());
        b.Retire(_clock.UtcNow);
        await _db.SaveChangesAsync(ct);
        return b.ToResponse();
    }

    public async Task<BundleItemResponse> AddItemAsync(Guid bundleId, AddBundleItemRequest request, CancellationToken ct)
    {
        await _addItemValidator.ValidateAndThrowAsync(request, ct);

        var bundle = await _db.Bundles.FindAsync(new object[] { bundleId }, ct)
                     ?? throw new NotFoundException("Bundle", bundleId.ToString());

        if (request.ProductId.HasValue)
        {
            var p = await _db.Products.FindAsync(new object[] { request.ProductId.Value }, ct)
                    ?? throw new NotFoundException("Product", request.ProductId.Value.ToString());
            if (p.Status == CatalogStatus.Retired)
                throw new InvalidRelationshipException("Bundle cannot include a retired product.");
        }
        else if (request.PlanId.HasValue)
        {
            var pl = await _db.Plans.FindAsync(new object[] { request.PlanId.Value }, ct)
                     ?? throw new NotFoundException("Plan", request.PlanId.Value.ToString());
            if (pl.Status == CatalogStatus.Retired)
                throw new InvalidRelationshipException("Bundle cannot include a retired plan.");
        }
        else if (request.AddonId.HasValue)
        {
            var ad = await _db.Addons.FindAsync(new object[] { request.AddonId.Value }, ct)
                     ?? throw new NotFoundException("Addon", request.AddonId.Value.ToString());
            if (ad.Status == CatalogStatus.Retired)
                throw new InvalidRelationshipException("Bundle cannot include a retired add-on.");
        }

        BundleItem item;
        try
        {
            item = BundleItem.Create(bundleId, request.ProductId, request.PlanId, request.AddonId, _clock.UtcNow);
        }
        catch (InvalidOperationException ex)
        {
            throw new InvalidRelationshipException(ex.Message);
        }

        _db.BundleItems.Add(item);
        await _db.SaveChangesAsync(ct);
        return item.ToResponse();
    }

    public async Task<IReadOnlyList<BundleItemResponse>> ListItemsAsync(Guid bundleId, CancellationToken ct)
    {
        if (!await _db.Bundles.AnyAsync(b => b.Id == bundleId, ct))
            throw new NotFoundException("Bundle", bundleId.ToString());
        var items = await _db.BundleItems.AsNoTracking().Where(i => i.BundleId == bundleId).ToListAsync(ct);
        return items.Select(CatalogMappers.ToResponse).ToList();
    }

    public async Task RemoveItemAsync(Guid bundleId, Guid itemId, CancellationToken ct)
    {
        var item = await _db.BundleItems.FirstOrDefaultAsync(i => i.BundleId == bundleId && i.Id == itemId, ct)
                   ?? throw new NotFoundException("BundleItem", itemId.ToString());
        _db.BundleItems.Remove(item);
        await _db.SaveChangesAsync(ct);
    }
}
