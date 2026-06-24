using Commerce.Domain.Catalog.Enums;

namespace Commerce.Contracts.Catalog;

public sealed record CreateAddonRequest(
    Guid? ProductId,
    string Key,
    string Name,
    string? Description);

public sealed record UpdateAddonRequest(
    string Name,
    string? Description);

public sealed record AddonResponse(
    Guid Id,
    Guid? ProductId,
    string Key,
    string Name,
    string? Description,
    CatalogStatus Status,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);
