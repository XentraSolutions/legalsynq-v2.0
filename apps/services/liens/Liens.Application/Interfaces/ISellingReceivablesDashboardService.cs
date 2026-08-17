using Liens.Application.DTOs;

namespace Liens.Application.Interfaces;

public interface ISellingReceivablesDashboardService
{
    Task<SellingReceivablesDashboardResponse> GetAsync(
        Guid tenantId,
        Guid sellerOrgId,
        SellingReceivablesDashboardRequest request,
        CancellationToken ct = default);
}
