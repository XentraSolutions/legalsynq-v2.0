using Commerce.Domain.Catalog.Enums;

namespace Commerce.Contracts.Catalog;

public sealed record CreateProductRequest(
    string Key,
    string Name,
    string? Description,
    int SortOrder = 0);

public sealed record UpdateProductRequest(
    string Name,
    string? Description,
    int SortOrder = 0);

public sealed record ProductResponse(
    Guid Id,
    string Key,
    string Name,
    string? Description,
    CatalogStatus Status,
    int SortOrder,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);
