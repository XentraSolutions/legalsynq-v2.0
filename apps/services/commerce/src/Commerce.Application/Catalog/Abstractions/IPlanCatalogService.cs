using Commerce.Contracts.Catalog;

namespace Commerce.Application.Catalog.Abstractions;

public interface IPlanCatalogService
{
    Task<PlanResponse> CreateAsync(CreatePlanRequest request, CancellationToken ct);
    Task<IReadOnlyList<PlanResponse>> ListAsync(CancellationToken ct);
    Task<PlanResponse> GetAsync(Guid id, CancellationToken ct);
    Task<PlanResponse> UpdateAsync(Guid id, UpdatePlanRequest request, CancellationToken ct);
    Task<PlanResponse> ActivateAsync(Guid id, CancellationToken ct);
    Task<PlanResponse> RetireAsync(Guid id, CancellationToken ct);

    Task<PlanFeatureResponse> AddFeatureAsync(Guid planId, AddPlanFeatureRequest request, CancellationToken ct);
    Task<IReadOnlyList<PlanFeatureResponse>> ListFeaturesAsync(Guid planId, CancellationToken ct);
    Task RemoveFeatureAsync(Guid planId, Guid featureId, CancellationToken ct);
}
