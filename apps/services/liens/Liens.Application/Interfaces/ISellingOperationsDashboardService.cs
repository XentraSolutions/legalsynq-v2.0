using Liens.Application.DTOs;

namespace Liens.Application.Interfaces;

public interface ISellingOperationsDashboardService
{
    Task<SellingOperationsDashboardResponse> GetAsync(
        Guid tenantId,
        Guid sellerOrgId,
        SellingOperationsDashboardQuery query,
        CancellationToken ct = default);
}
