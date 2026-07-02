using Liens.Application.DTOs;

namespace Liens.Application.Interfaces;

public interface ISellingPortfolioService
{
    Task<PaginatedResult<SellingPortfolioResponse>> SearchAsync(
        Guid tenantId,
        Guid? sellerOrgId,
        string? search,
        string? status,
        Guid? buyerOrgId,
        int page,
        int pageSize,
        CancellationToken ct = default);

    Task<SellingPortfolioResponse?> GetByIdAsync(
        Guid tenantId,
        Guid id,
        Guid sellerOrgId,
        CancellationToken ct = default);

    Task<SellingPortfolioResponse> CreateAsync(
        Guid tenantId,
        Guid sellerOrgId,
        Guid actingUserId,
        CreateSellingPortfolioRequest request,
        CancellationToken ct = default);

    Task<SellingPortfolioResponse> UpdateAsync(
        Guid tenantId,
        Guid id,
        Guid sellerOrgId,
        Guid actingUserId,
        UpdateSellingPortfolioRequest request,
        CancellationToken ct = default);

    Task<AddSellingPortfolioLiensResponse> AddLiensAsync(
        Guid tenantId,
        Guid id,
        Guid sellerOrgId,
        Guid actingUserId,
        AddSellingPortfolioLiensRequest request,
        CancellationToken ct = default);

    Task<RemoveSellingPortfolioLiensResponse> RemoveLiensAsync(
        Guid tenantId,
        Guid id,
        Guid sellerOrgId,
        Guid actingUserId,
        RemoveSellingPortfolioLiensRequest request,
        CancellationToken ct = default);

    Task<SellingPortfolioResponse> AddBuyersAsync(
        Guid tenantId,
        Guid id,
        Guid sellerOrgId,
        Guid actingUserId,
        AddSellingPortfolioBuyersRequest request,
        CancellationToken ct = default);

    Task<SellingPortfolioResponse> TransitionStatusAsync(
        Guid tenantId,
        Guid id,
        Guid sellerOrgId,
        Guid actingUserId,
        TransitionSellingPortfolioStatusRequest request,
        CancellationToken ct = default);

    Task<SellingPortfolioResponse> PublishAsync(
        Guid tenantId,
        Guid id,
        Guid sellerOrgId,
        Guid actingUserId,
        string? notes = null,
        CancellationToken ct = default);

    Task<SellingPortfolioResponse> WithdrawAsync(
        Guid tenantId,
        Guid id,
        Guid sellerOrgId,
        Guid actingUserId,
        string? notes = null,
        CancellationToken ct = default);

    Task<IReadOnlyList<SellingPortfolioStatusHistoryResponse>> GetStatusHistoryAsync(
        Guid tenantId,
        Guid id,
        Guid sellerOrgId,
        CancellationToken ct = default);

    Task<IReadOnlyList<SellingPortfolioActivityResponse>> GetActivityAsync(
        Guid tenantId,
        Guid id,
        Guid sellerOrgId,
        CancellationToken ct = default);

    Task<SellingPortfolioAnalyticsResponse> GetAnalyticsAsync(
        Guid tenantId,
        Guid id,
        Guid sellerOrgId,
        CancellationToken ct = default);

    Task<SendLienBuyerEmailResponse> SendBuyerEmailAsync(
        Guid tenantId,
        Guid portfolioId,
        string lienIdOrCode,
        Guid sellerOrgId,
        Guid actingUserId,
        SendLienBuyerEmailRequest request,
        CancellationToken ct = default);
}
