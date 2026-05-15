using Commerce.Domain.Catalog.Enums;

namespace Commerce.Contracts.Catalog;

public sealed record CreatePlanRequest(
    Guid? ProductId,
    string Key,
    string Name,
    string? Description,
    BillingInterval BillingInterval,
    int? TrialDays,
    int SortOrder = 0);

public sealed record UpdatePlanRequest(
    string Name,
    string? Description,
    BillingInterval BillingInterval,
    int? TrialDays,
    int SortOrder = 0);

public sealed record PlanResponse(
    Guid Id,
    Guid? ProductId,
    string Key,
    string Name,
    string? Description,
    CatalogStatus Status,
    BillingInterval BillingInterval,
    int? TrialDays,
    int SortOrder,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);
