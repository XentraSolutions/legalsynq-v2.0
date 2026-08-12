using Liens.Application.DTOs;

namespace Liens.Application.Interfaces;

public interface ISellingAnalyticsService
{
    Task<SellingAnalyticsOverviewResponse> GetOverviewAsync(
        Guid tenantId,
        Guid sellerOrgId,
        SellingAnalyticsFilter filter,
        CancellationToken ct = default);

    Task<SellingAnalyticsStatusBreakdownResponse> GetStatusBreakdownAsync(
        Guid tenantId,
        Guid sellerOrgId,
        SellingAnalyticsFilter filter,
        CancellationToken ct = default);

    Task<SellingAnalyticsFunnelResponse> GetFunnelAsync(
        Guid tenantId,
        Guid sellerOrgId,
        SellingAnalyticsFilter filter,
        CancellationToken ct = default);

    Task<SellingAnalyticsTimeseriesResponse> GetTimeseriesAsync(
        Guid tenantId,
        Guid sellerOrgId,
        SellingAnalyticsFilter filter,
        CancellationToken ct = default);

    Task<SellingAnalyticsOffersResponse> GetOffersAsync(
        Guid tenantId,
        Guid sellerOrgId,
        SellingAnalyticsFilter filter,
        CancellationToken ct = default);

    Task<SellingAnalyticsBuyerPerformanceResponse> GetBuyerPerformanceAsync(
        Guid tenantId,
        Guid sellerOrgId,
        SellingAnalyticsFilter filter,
        CancellationToken ct = default);

    Task<SellingAnalyticsAgingResponse> GetAgingAsync(
        Guid tenantId,
        Guid sellerOrgId,
        SellingAnalyticsFilter filter,
        CancellationToken ct = default);

    Task<SellingAnalyticsConcentrationResponse> GetConcentrationAsync(
        Guid tenantId,
        Guid sellerOrgId,
        SellingAnalyticsFilter filter,
        string dimension,
        CancellationToken ct = default);

    Task<SellingAnalyticsFilterOptionsResponse> GetFilterOptionsAsync(
        Guid tenantId,
        Guid sellerOrgId,
        SellingAnalyticsFilter filter,
        CancellationToken ct = default);

    Task<SellingLienAnalyticsResponse> GetLienAnalyticsAsync(
        Guid tenantId,
        Guid sellerOrgId,
        Guid lienId,
        CancellationToken ct = default);

    Task<SellingAnalyticsExportResult> ExportAsync(
        Guid tenantId,
        Guid sellerOrgId,
        SellingAnalyticsExportRequest request,
        CancellationToken ct = default);
}
