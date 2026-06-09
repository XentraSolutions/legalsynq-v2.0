using Commerce.Application.Catalog.Abstractions;
using Commerce.Application.Common.Exceptions;
using Commerce.Application.Common.Time;
using Commerce.Contracts.Catalog;
using Commerce.Domain.Catalog;
using Commerce.Infrastructure.Catalog.Mapping;
using Commerce.Infrastructure.Persistence;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Commerce.Infrastructure.Catalog.Services;

public sealed class AddonCatalogService : IAddonCatalogService
{
    private readonly CommerceDbContext _db;
    private readonly IClock _clock;
    private readonly IValidator<CreateAddonRequest> _createValidator;
    private readonly IValidator<UpdateAddonRequest> _updateValidator;

    public AddonCatalogService(
        CommerceDbContext db,
        IClock clock,
        IValidator<CreateAddonRequest> createValidator,
        IValidator<UpdateAddonRequest> updateValidator)
    {
        _db = db;
        _clock = clock;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
    }

    public async Task<AddonResponse> CreateAsync(CreateAddonRequest request, CancellationToken ct)
    {
        await _createValidator.ValidateAndThrowAsync(request, ct);

        if (request.ProductId.HasValue &&
            !await _db.Products.AnyAsync(p => p.Id == request.ProductId.Value, ct))
        {
            throw new NotFoundException("Product", request.ProductId.Value.ToString());
        }

        var key = CatalogKey.Normalize(request.Key);
        if (await _db.Addons.AnyAsync(a => a.Key == key, ct))
            throw new DuplicateKeyException("Addon", key);

        var addon = Addon.Create(request.ProductId, request.Key, request.Name, request.Description, _clock.UtcNow);
        _db.Addons.Add(addon);
        await _db.SaveChangesAsync(ct);
        return addon.ToResponse();
    }

    public async Task<IReadOnlyList<AddonResponse>> ListAsync(CancellationToken ct)
    {
        var items = await _db.Addons.AsNoTracking().OrderBy(a => a.Key).ToListAsync(ct);
        return items.Select(CatalogMappers.ToResponse).ToList();
    }

    public async Task<AddonResponse> GetAsync(Guid id, CancellationToken ct)
    {
        var a = await _db.Addons.FindAsync(new object[] { id }, ct)
                ?? throw new NotFoundException("Addon", id.ToString());
        return a.ToResponse();
    }

    public async Task<AddonResponse> UpdateAsync(Guid id, UpdateAddonRequest request, CancellationToken ct)
    {
        await _updateValidator.ValidateAndThrowAsync(request, ct);
        var a = await _db.Addons.FindAsync(new object[] { id }, ct)
                ?? throw new NotFoundException("Addon", id.ToString());
        a.Update(request.Name, request.Description, _clock.UtcNow);
        await _db.SaveChangesAsync(ct);
        return a.ToResponse();
    }

    public async Task<AddonResponse> ActivateAsync(Guid id, CancellationToken ct)
    {
        var a = await _db.Addons.FindAsync(new object[] { id }, ct)
                ?? throw new NotFoundException("Addon", id.ToString());
        try { a.Activate(_clock.UtcNow); }
        catch (InvalidOperationException ex) { throw new InvalidStateTransitionException(ex.Message); }
        await _db.SaveChangesAsync(ct);
        return a.ToResponse();
    }

    public async Task<AddonResponse> RetireAsync(Guid id, CancellationToken ct)
    {
        var a = await _db.Addons.FindAsync(new object[] { id }, ct)
                ?? throw new NotFoundException("Addon", id.ToString());
        a.Retire(_clock.UtcNow);
        await _db.SaveChangesAsync(ct);
        return a.ToResponse();
    }
}
