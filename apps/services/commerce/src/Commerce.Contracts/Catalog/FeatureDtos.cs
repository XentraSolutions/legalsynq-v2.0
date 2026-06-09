using Commerce.Domain.Catalog.Enums;

namespace Commerce.Contracts.Catalog;

public sealed record CreateFeatureRequest(
    string Key,
    string Name,
    string? Description,
    FeatureType FeatureType);

public sealed record UpdateFeatureRequest(
    string Name,
    string? Description,
    FeatureType FeatureType);

public sealed record FeatureResponse(
    Guid Id,
    Guid ProductId,
    string Key,
    string Name,
    string? Description,
    FeatureType FeatureType,
    CatalogStatus Status,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);
