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

public sealed class FeatureCatalogService : IFeatureCatalogService
{
    private readonly CommerceDbContext _db;
    private readonly IClock _clock;
    private readonly IValidator<CreateFeatureRequest> _createValidator;
    private readonly IValidator<UpdateFeatureRequest> _updateValidator;

    public FeatureCatalogService(
        CommerceDbContext db,
        IClock clock,
        IValidator<CreateFeatureRequest> createValidator,
        IValidator<UpdateFeatureRequest> updateValidator)
    {
        _db = db;
        _clock = clock;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
    }

    public async Task<FeatureResponse> CreateAsync(Guid productId, CreateFeatureRequest request, CancellationToken ct)
    {
        await _createValidator.ValidateAndThrowAsync(request, ct);
        var product = await _db.Products.FindAsync(new object[] { productId }, ct)
                      ?? throw new NotFoundException("Product", productId.ToString());

        if (product.Status == CatalogStatus.Retired)
            throw new InvalidRelationshipException("Cannot add a feature to a retired product.");

        var key = CatalogKey.Normalize(request.Key);
        if (await _db.Features.AnyAsync(f => f.ProductId == productId && f.Key == key, ct))
            throw new DuplicateKeyException("Feature", key);

        var feature = Feature.Create(productId, request.Key, request.Name, request.Description, request.FeatureType, _clock.UtcNow);
        _db.Features.Add(feature);
        await _db.SaveChangesAsync(ct);
        return feature.ToResponse();
    }

    public async Task<IReadOnlyList<FeatureResponse>> ListByProductAsync(Guid productId, CancellationToken ct)
    {
        if (!await _db.Products.AnyAsync(p => p.Id == productId, ct))
            throw new NotFoundException("Product", productId.ToString());
        var items = await _db.Features.AsNoTracking().Where(f => f.ProductId == productId).OrderBy(f => f.Key).ToListAsync(ct);
        return items.Select(CatalogMappers.ToResponse).ToList();
    }

    public async Task<FeatureResponse> UpdateAsync(Guid id, UpdateFeatureRequest request, CancellationToken ct)
    {
        await _updateValidator.ValidateAndThrowAsync(request, ct);
        var f = await _db.Features.FindAsync(new object[] { id }, ct)
                ?? throw new NotFoundException("Feature", id.ToString());
        f.Update(request.Name, request.Description, request.FeatureType, _clock.UtcNow);
        await _db.SaveChangesAsync(ct);
        return f.ToResponse();
    }

    public async Task<FeatureResponse> ActivateAsync(Guid id, CancellationToken ct)
    {
        var f = await _db.Features.FindAsync(new object[] { id }, ct)
                ?? throw new NotFoundException("Feature", id.ToString());
        try { f.Activate(_clock.UtcNow); }
        catch (InvalidOperationException ex) { throw new InvalidStateTransitionException(ex.Message); }
        await _db.SaveChangesAsync(ct);
        return f.ToResponse();
    }

    public async Task<FeatureResponse> RetireAsync(Guid id, CancellationToken ct)
    {
        var f = await _db.Features.FindAsync(new object[] { id }, ct)
                ?? throw new NotFoundException("Feature", id.ToString());
        f.Retire(_clock.UtcNow);
        await _db.SaveChangesAsync(ct);
        return f.ToResponse();
    }
}
