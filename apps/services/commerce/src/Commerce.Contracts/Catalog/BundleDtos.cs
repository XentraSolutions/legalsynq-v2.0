using Commerce.Domain.Catalog.Enums;

namespace Commerce.Contracts.Catalog;

public sealed record CreateBundleRequest(
    string Key,
    string Name,
    string? Description);

public sealed record UpdateBundleRequest(
    string Name,
    string? Description);

public sealed record BundleResponse(
    Guid Id,
    string Key,
    string Name,
    string? Description,
    CatalogStatus Status,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);

public sealed record AddBundleItemRequest(
    Guid? ProductId,
    Guid? PlanId,
    Guid? AddonId);

public sealed record BundleItemResponse(
    Guid Id,
    Guid BundleId,
    Guid? ProductId,
    Guid? PlanId,
    Guid? AddonId,
    DateTime CreatedAtUtc);
