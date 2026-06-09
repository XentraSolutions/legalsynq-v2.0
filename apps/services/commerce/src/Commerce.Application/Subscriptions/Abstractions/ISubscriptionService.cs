using Commerce.Contracts.Subscriptions;

namespace Commerce.Application.Subscriptions.Abstractions;

public interface ISubscriptionService
{
    Task<SubscriptionResponse> CreateAsync(CreateSubscriptionRequest request, CancellationToken ct);
    Task<IReadOnlyList<SubscriptionResponse>> ListAsync(Guid? billingAccountId, CancellationToken ct);
    Task<SubscriptionResponse> GetAsync(Guid id, CancellationToken ct);
    Task<SubscriptionResponse> ActivateAsync(Guid id, CancellationToken ct);
    Task<SubscriptionResponse> CancelAsync(Guid id, CancelSubscriptionRequest request, CancellationToken ct);
    Task<SubscriptionResponse> SuspendAsync(Guid id, CancellationToken ct);
    Task<SubscriptionResponse> ReactivateAsync(Guid id, CancellationToken ct);
    Task<SubscriptionResponse> RenewAsync(Guid id, RenewSubscriptionRequest? request, CancellationToken ct);
    Task<SubscriptionResponse> ChangePlanAsync(Guid id, ChangeSubscriptionPlanRequest request, CancellationToken ct);
    Task<IReadOnlyList<SubscriptionChangeResponse>> ListChangesAsync(Guid id, CancellationToken ct);
}
