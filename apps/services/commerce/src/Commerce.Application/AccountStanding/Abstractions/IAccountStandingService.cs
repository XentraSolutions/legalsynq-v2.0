using Commerce.Contracts.AccountStanding;

namespace Commerce.Application.AccountStanding.Abstractions;

public interface IAccountStandingService
{
    Task<AccountStandingResponse> EvaluateAsync(Guid billingAccountId, CancellationToken ct);
    Task<AccountStandingResponse> GetAsync(Guid billingAccountId, CancellationToken ct);
}
