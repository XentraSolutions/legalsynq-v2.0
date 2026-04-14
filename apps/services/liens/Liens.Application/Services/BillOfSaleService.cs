using Liens.Application.DTOs;
using Liens.Application.Interfaces;
using Liens.Application.Repositories;
using Liens.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace Liens.Application.Services;

public sealed class BillOfSaleService : IBillOfSaleService
{
    private readonly IBillOfSaleRepository _bosRepo;
    private readonly ILogger<BillOfSaleService> _logger;

    public BillOfSaleService(
        IBillOfSaleRepository bosRepo,
        ILogger<BillOfSaleService> logger)
    {
        _bosRepo = bosRepo;
        _logger = logger;
    }

    public async Task<BillOfSaleResponse?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default)
    {
        var entity = await _bosRepo.GetByIdAsync(tenantId, id, ct);
        return entity is null ? null : MapToResponse(entity);
    }

    public async Task<BillOfSaleResponse?> GetByBillOfSaleNumberAsync(Guid tenantId, string billOfSaleNumber, CancellationToken ct = default)
    {
        var entity = await _bosRepo.GetByBillOfSaleNumberAsync(tenantId, billOfSaleNumber, ct);
        return entity is null ? null : MapToResponse(entity);
    }

    public async Task<PaginatedResult<BillOfSaleResponse>> SearchAsync(
        Guid tenantId, Guid? lienId, string? status,
        Guid? buyerOrgId, Guid? sellerOrgId,
        string? search,
        int page, int pageSize,
        CancellationToken ct = default)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 20;
        if (pageSize > 100) pageSize = 100;

        var (items, totalCount) = await _bosRepo.SearchAsync(
            tenantId, lienId, status, buyerOrgId, sellerOrgId, search, page, pageSize, ct);

        return new PaginatedResult<BillOfSaleResponse>
        {
            Items = items.Select(MapToResponse).ToList(),
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
        };
    }

    public async Task<List<BillOfSaleResponse>> GetByLienIdAsync(Guid tenantId, Guid lienId, CancellationToken ct = default)
    {
        var items = await _bosRepo.GetByLienIdAsync(tenantId, lienId, ct);
        return items.Select(MapToResponse).ToList();
    }

    private static BillOfSaleResponse MapToResponse(BillOfSale entity)
    {
        return new BillOfSaleResponse
        {
            Id = entity.Id,
            BillOfSaleNumber = entity.BillOfSaleNumber,
            ExternalReference = entity.ExternalReference,
            Status = entity.Status,
            LienId = entity.LienId,
            LienOfferId = entity.LienOfferId,
            SellerOrgId = entity.SellerOrgId,
            BuyerOrgId = entity.BuyerOrgId,
            PurchaseAmount = entity.PurchaseAmount,
            OriginalLienAmount = entity.OriginalLienAmount,
            DiscountPercent = entity.DiscountPercent,
            SellerContactName = entity.SellerContactName,
            BuyerContactName = entity.BuyerContactName,
            Terms = entity.Terms,
            Notes = entity.Notes,
            DocumentId = entity.DocumentId,
            IssuedAtUtc = entity.IssuedAtUtc,
            ExecutedAtUtc = entity.ExecutedAtUtc,
            EffectiveAtUtc = entity.EffectiveAtUtc,
            CancelledAtUtc = entity.CancelledAtUtc,
            CreatedAtUtc = entity.CreatedAtUtc,
            UpdatedAtUtc = entity.UpdatedAtUtc,
        };
    }
}
