using Liens.Application.DTOs;

namespace Liens.Application.Interfaces;

public interface ISellingDashboardService
{
    Task<SellingDashboardResponse> GetAsync(
        Guid tenantId,
        Guid sellerOrgId,
        SellingDashboardQuery query,
        CancellationToken ct = default);
}
