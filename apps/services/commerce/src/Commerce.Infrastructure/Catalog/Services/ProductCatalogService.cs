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

public sealed class ProductCatalogService : IProductCatalogService
{
    private readonly CommerceDbContext _db;
    private readonly IClock _clock;
    private readonly IValidator<CreateProductRequest> _createValidator;
    private readonly IValidator<UpdateProductRequest> _updateValidator;

    public ProductCatalogService(
        CommerceDbContext db,
        IClock clock,
        IValidator<CreateProductRequest> createValidator,
        IValidator<UpdateProductRequest> updateValidator)
    {
        _db = db;
        _clock = clock;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
    }

    public async Task<ProductResponse> CreateAsync(CreateProductRequest request, CancellationToken ct)
    {
        await _createValidator.ValidateAndThrowAsync(request, ct);
        var key = CatalogKey.Normalize(request.Key);
        if (await _db.Products.AnyAsync(p => p.Key == key, ct))
            throw new DuplicateKeyException("Product", key);

        var product = Product.Create(request.Key, request.Name, request.Description, request.SortOrder, _clock.UtcNow);
        _db.Products.Add(product);
        await _db.SaveChangesAsync(ct);
        return product.ToResponse();
    }

    public async Task<IReadOnlyList<ProductResponse>> ListAsync(CancellationToken ct)
    {
        var items = await _db.Products.AsNoTracking().OrderBy(p => p.SortOrder).ThenBy(p => p.Key).ToListAsync(ct);
        return items.Select(CatalogMappers.ToResponse).ToList();
    }

    public async Task<ProductResponse> GetAsync(Guid id, CancellationToken ct)
    {
        var p = await _db.Products.FindAsync(new object[] { id }, ct)
                ?? throw new NotFoundException("Product", id.ToString());
        return p.ToResponse();
    }

    public async Task<ProductResponse> UpdateAsync(Guid id, UpdateProductRequest request, CancellationToken ct)
    {
        await _updateValidator.ValidateAndThrowAsync(request, ct);
        var p = await _db.Products.FindAsync(new object[] { id }, ct)
                ?? throw new NotFoundException("Product", id.ToString());
        p.Update(request.Name, request.Description, request.SortOrder, _clock.UtcNow);
        await _db.SaveChangesAsync(ct);
        return p.ToResponse();
    }

    public async Task<ProductResponse> ActivateAsync(Guid id, CancellationToken ct)
    {
        var p = await _db.Products.FindAsync(new object[] { id }, ct)
                ?? throw new NotFoundException("Product", id.ToString());
        try { p.Activate(_clock.UtcNow); }
        catch (InvalidOperationException ex) { throw new InvalidStateTransitionException(ex.Message); }
        await _db.SaveChangesAsync(ct);
        return p.ToResponse();
    }

    public async Task<ProductResponse> RetireAsync(Guid id, CancellationToken ct)
    {
        var p = await _db.Products.FindAsync(new object[] { id }, ct)
                ?? throw new NotFoundException("Product", id.ToString());
        p.Retire(_clock.UtcNow);
        await _db.SaveChangesAsync(ct);
        return p.ToResponse();
    }
}
