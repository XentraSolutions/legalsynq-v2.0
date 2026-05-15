using Commerce.Contracts.Admin;

namespace Commerce.Application.Admin.Abstractions;

/// <summary>
/// Read-only admin/dashboard query surface. All methods are safe to call
/// repeatedly and never mutate state. Implementations must use the
/// <c>CommerceDbContext</c> through pure projections.
/// </summary>
public interface IAdminDashboardService
{
    Task<AdminDashboardSummaryResponse> GetSummaryAsync(CancellationToken ct);

    Task<RevenueSummaryResponse> GetRevenueSummaryAsync(CancellationToken ct);

    Task<AccountStandingSummaryResponse> GetAccountStandingSummaryAsync(CancellationToken ct);

    Task<ProviderEventSummaryResponse> GetProviderEventSummaryAsync(CancellationToken ct);

    Task<RecentActivityResponse> GetRecentActivityAsync(int take, CancellationToken ct);
}
