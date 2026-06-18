using BuildingBlocks.Exceptions;
using Liens.Application.DTOs;
using Liens.Application.Interfaces;
using Liens.Application.Repositories;
using Liens.Domain.Entities;
using Liens.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace Liens.Application.Services;

public sealed class SellingPortfolioService : ISellingPortfolioService
{
    private readonly ISellingPortfolioRepository _portfolioRepo;
    private readonly ILienRepository _lienRepo;
    private readonly ICaseRepository _caseRepo;
    private readonly IAuditPublisher _audit;
    private readonly ILogger<SellingPortfolioService> _logger;

    public SellingPortfolioService(
        ISellingPortfolioRepository portfolioRepo,
        ILienRepository lienRepo,
        ICaseRepository caseRepo,
        IAuditPublisher audit,
        ILogger<SellingPortfolioService> logger)
    {
        _portfolioRepo = portfolioRepo;
        _lienRepo = lienRepo;
        _caseRepo = caseRepo;
        _audit = audit;
        _logger = logger;
    }

    public async Task<PaginatedResult<SellingPortfolioResponse>> SearchAsync(
        Guid tenantId,
        Guid? sellerOrgId,
        string? search,
        string? status,
        Guid? buyerOrgId,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 20;
        if (pageSize > 100) pageSize = 100;

        if (!string.IsNullOrWhiteSpace(status) && !SellingPortfolioStatus.All.Contains(status))
            throw new ValidationException("One or more fields are invalid.",
                new Dictionary<string, string[]> { ["status"] = [$"Invalid selling portfolio status: '{status}'."] });

        var (items, totalCount) = await _portfolioRepo.SearchAsync(
            tenantId, sellerOrgId, search, status, buyerOrgId, page, pageSize, ct);

        return new PaginatedResult<SellingPortfolioResponse>
        {
            Items = items.Select(MapToResponse).ToList(),
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
        };
    }

    public async Task<SellingPortfolioResponse?> GetByIdAsync(
        Guid tenantId,
        Guid id,
        Guid sellerOrgId,
        CancellationToken ct = default)
    {
        var entity = await _portfolioRepo.GetByIdAsync(tenantId, id, ct);
        if (entity is not null)
            EnsureSellerPortfolio(entity, sellerOrgId);

        return entity is null ? null : MapToResponse(entity);
    }

    public async Task<SellingPortfolioResponse> CreateAsync(
        Guid tenantId,
        Guid sellerOrgId,
        Guid actingUserId,
        CreateSellingPortfolioRequest request,
        CancellationToken ct = default)
    {
        ValidateCreateRequest(request);

        var portfolioNumber = request.PortfolioNumber.Trim();
        var existing = await _portfolioRepo.GetByPortfolioNumberAsync(tenantId, portfolioNumber, ct);
        if (existing is not null)
            throw new ConflictException(
                $"A selling portfolio with number '{portfolioNumber}' already exists.",
                "SELLING_PORTFOLIO_NUMBER_DUPLICATE");

        var portfolio = SellingPortfolio.Create(
            tenantId,
            sellerOrgId,
            portfolioNumber,
            request.Name,
            actingUserId,
            request.Description);

        portfolio.AddInitialStatusHistory(actingUserId);

        foreach (var lienId in request.LienIds.Distinct())
        {
            var snapshot = await CreateLienSnapshotAsync(tenantId, sellerOrgId, portfolio.Id, lienId, actingUserId, ct);
            portfolio.AddLien(snapshot, actingUserId);
        }

        foreach (var buyerOrgId in request.BuyerOrgIds.Distinct())
            portfolio.AddBuyer(buyerOrgId, actingUserId);

        await _portfolioRepo.AddAsync(portfolio, ct);

        _logger.LogInformation(
            "Selling portfolio created: {PortfolioId} Number={PortfolioNumber} Tenant={TenantId}",
            portfolio.Id, portfolio.PortfolioNumber, tenantId);

        _audit.Publish(
            eventType: "liens.selling_portfolio.created",
            action: "create",
            description: $"Selling portfolio '{portfolio.PortfolioNumber}' created",
            tenantId: tenantId,
            actorUserId: actingUserId,
            entityType: "SellingPortfolio",
            entityId: portfolio.Id.ToString());

        return MapToResponse(portfolio);
    }

    public async Task<SellingPortfolioResponse> UpdateAsync(
        Guid tenantId,
        Guid id,
        Guid sellerOrgId,
        Guid actingUserId,
        UpdateSellingPortfolioRequest request,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new ValidationException("One or more required fields are missing or invalid.",
                new Dictionary<string, string[]> { ["name"] = ["Name is required."] });

        var portfolio = await RequirePortfolioAsync(tenantId, id, ct);
        EnsureSellerPortfolio(portfolio, sellerOrgId);
        portfolio.Update(request.Name, actingUserId, request.Description);
        await _portfolioRepo.UpdateAsync(portfolio, ct);

        _audit.Publish(
            eventType: "liens.selling_portfolio.updated",
            action: "update",
            description: $"Selling portfolio '{portfolio.PortfolioNumber}' updated",
            tenantId: tenantId,
            actorUserId: actingUserId,
            entityType: "SellingPortfolio",
            entityId: portfolio.Id.ToString());

        return MapToResponse(portfolio);
    }

    public async Task<SellingPortfolioResponse> AddLiensAsync(
        Guid tenantId,
        Guid id,
        Guid sellerOrgId,
        Guid actingUserId,
        AddSellingPortfolioLiensRequest request,
        CancellationToken ct = default)
    {
        if (request.LienIds.Count == 0)
            throw new ValidationException("One or more required fields are missing or invalid.",
                new Dictionary<string, string[]> { ["lienIds"] = ["At least one lien id is required."] });

        var portfolio = await RequirePortfolioAsync(tenantId, id, ct);
        EnsureSellerPortfolio(portfolio, sellerOrgId);

        foreach (var lienId in request.LienIds.Distinct())
        {
            var snapshot = await CreateLienSnapshotAsync(tenantId, sellerOrgId, portfolio.Id, lienId, actingUserId, ct);
            portfolio.AddLien(snapshot, actingUserId);
        }

        await _portfolioRepo.UpdateAsync(portfolio, ct);
        return MapToResponse(portfolio);
    }

    public async Task<SellingPortfolioResponse> AddBuyersAsync(
        Guid tenantId,
        Guid id,
        Guid sellerOrgId,
        Guid actingUserId,
        AddSellingPortfolioBuyersRequest request,
        CancellationToken ct = default)
    {
        if (request.BuyerOrgIds.Count == 0)
            throw new ValidationException("One or more required fields are missing or invalid.",
                new Dictionary<string, string[]> { ["buyerOrgIds"] = ["At least one buyer organization id is required."] });

        var portfolio = await RequirePortfolioAsync(tenantId, id, ct);
        EnsureSellerPortfolio(portfolio, sellerOrgId);

        foreach (var buyerOrgId in request.BuyerOrgIds.Distinct())
            portfolio.AddBuyer(buyerOrgId, actingUserId);

        await _portfolioRepo.UpdateAsync(portfolio, ct);
        return MapToResponse(portfolio);
    }

    public async Task<SellingPortfolioResponse> TransitionStatusAsync(
        Guid tenantId,
        Guid id,
        Guid sellerOrgId,
        Guid actingUserId,
        TransitionSellingPortfolioStatusRequest request,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Status))
            throw new ValidationException("One or more required fields are missing or invalid.",
                new Dictionary<string, string[]> { ["status"] = ["Status is required."] });

        var portfolio = await RequirePortfolioAsync(tenantId, id, ct);
        EnsureSellerPortfolio(portfolio, sellerOrgId);
        var fromStatus = portfolio.Status;
        portfolio.TransitionStatus(request.Status.Trim(), actingUserId, request.Notes);
        await _portfolioRepo.UpdateAsync(portfolio, ct);

        _audit.Publish(
            eventType: "liens.selling_portfolio.status_changed",
            action: "transition",
            description: $"Selling portfolio '{portfolio.PortfolioNumber}' transitioned from {fromStatus} to {portfolio.Status}",
            tenantId: tenantId,
            actorUserId: actingUserId,
            entityType: "SellingPortfolio",
            entityId: portfolio.Id.ToString(),
            metadata: $"{{\"fromStatus\":\"{fromStatus}\",\"toStatus\":\"{portfolio.Status}\"}}");

        return MapToResponse(portfolio);
    }

    public async Task<IReadOnlyList<SellingPortfolioStatusHistoryResponse>> GetStatusHistoryAsync(
        Guid tenantId,
        Guid id,
        Guid sellerOrgId,
        CancellationToken ct = default)
    {
        var portfolio = await RequirePortfolioAsync(tenantId, id, ct);
        EnsureSellerPortfolio(portfolio, sellerOrgId);

        var history = await _portfolioRepo.GetStatusHistoryAsync(tenantId, id, ct);
        return history.Select(MapStatusHistory).ToList();
    }

    private static void ValidateCreateRequest(CreateSellingPortfolioRequest request)
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(request.PortfolioNumber))
            errors.Add("portfolioNumber", ["Portfolio number is required."]);
        if (string.IsNullOrWhiteSpace(request.Name))
            errors.Add("name", ["Name is required."]);

        if (request.LienIds.Any(id => id == Guid.Empty))
            errors.Add("lienIds", ["Lien ids cannot contain empty values."]);

        if (request.BuyerOrgIds.Any(id => id == Guid.Empty))
            errors.Add("buyerOrgIds", ["Buyer organization ids cannot contain empty values."]);

        if (errors.Count > 0)
            throw new ValidationException("One or more required fields are missing or invalid.", errors);
    }

    private async Task<SellingPortfolio> RequirePortfolioAsync(Guid tenantId, Guid id, CancellationToken ct)
    {
        return await _portfolioRepo.GetByIdAsync(tenantId, id, ct)
            ?? throw new NotFoundException($"Selling portfolio '{id}' not found for tenant '{tenantId}'.");
    }

    private async Task<SellingPortfolioLien> CreateLienSnapshotAsync(
        Guid tenantId,
        Guid sellerOrgId,
        Guid portfolioId,
        Guid lienId,
        Guid actingUserId,
        CancellationToken ct)
    {
        var lien = await _lienRepo.GetByIdAsync(tenantId, lienId, ct)
            ?? throw new ValidationException("Referenced lien does not exist.",
                new Dictionary<string, string[]> { ["lienIds"] = [$"Lien '{lienId}' not found."] });

        if (lien.SellingOrgId != sellerOrgId && lien.OrgId != sellerOrgId)
            throw new ValidationException("Referenced lien is not owned by the seller organization.",
                new Dictionary<string, string[]> { ["lienIds"] = [$"Lien '{lienId}' is not owned by seller organization '{sellerOrgId}'."] });

        string? caseExternalId = null;
        if (lien.CaseId.HasValue)
        {
            var caseEntity = await _caseRepo.GetByIdAsync(tenantId, lien.CaseId.Value, ct);
            caseExternalId = caseEntity?.ExternalReference;
        }

        return SellingPortfolioLien.CreateSnapshot(tenantId, portfolioId, lien, caseExternalId, actingUserId);
    }

    private static void EnsureSellerPortfolio(SellingPortfolio portfolio, Guid sellerOrgId)
    {
        if (portfolio.SellerOrgId != sellerOrgId)
            throw new UnauthorizedAccessException("Selling portfolio does not belong to the current seller organization.");
    }

    private static SellingPortfolioResponse MapToResponse(SellingPortfolio entity)
    {
        return new SellingPortfolioResponse
        {
            Id = entity.Id,
            TenantId = entity.TenantId,
            SellerOrgId = entity.SellerOrgId,
            PortfolioNumber = entity.PortfolioNumber,
            Name = entity.Name,
            Description = entity.Description,
            Status = entity.Status,
            LienCount = entity.LienCount,
            OriginalAmountTotal = entity.OriginalAmountTotal,
            CurrentBalanceTotal = entity.CurrentBalanceTotal,
            OfferPriceTotal = entity.OfferPriceTotal,
            PublishedAtUtc = entity.PublishedAtUtc,
            ClosedAtUtc = entity.ClosedAtUtc,
            CreatedAtUtc = entity.CreatedAtUtc,
            UpdatedAtUtc = entity.UpdatedAtUtc,
            Liens = entity.Liens.Select(MapLien).ToList(),
            Buyers = entity.Buyers.Select(MapBuyer).ToList(),
        };
    }

    private static SellingPortfolioLienResponse MapLien(SellingPortfolioLien entity) => new()
    {
        Id = entity.Id,
        PortfolioId = entity.PortfolioId,
        LienId = entity.LienId,
        LienNumber = entity.LienNumber,
        LienExternalId = entity.LienExternalId,
        CaseId = entity.CaseId,
        CaseExternalId = entity.CaseExternalId,
        FacilityId = entity.FacilityId,
        LienType = entity.LienType,
        LienLifecycleStatus = entity.LienLifecycleStatus,
        OriginalAmount = entity.OriginalAmount,
        CurrentBalance = entity.CurrentBalance,
        OfferPrice = entity.OfferPrice,
        PurchasePrice = entity.PurchasePrice,
        PayoffAmount = entity.PayoffAmount,
        SubjectFirstName = entity.SubjectFirstName,
        SubjectLastName = entity.SubjectLastName,
        Jurisdiction = entity.Jurisdiction,
        IncidentDate = entity.IncidentDate,
        Description = entity.Description,
        CreatedAtUtc = entity.CreatedAtUtc,
        UpdatedAtUtc = entity.UpdatedAtUtc,
    };

    private static SellingPortfolioBuyerResponse MapBuyer(SellingPortfolioBuyer entity) => new()
    {
        Id = entity.Id,
        PortfolioId = entity.PortfolioId,
        BuyerOrgId = entity.BuyerOrgId,
        CreatedAtUtc = entity.CreatedAtUtc,
    };

    private static SellingPortfolioStatusHistoryResponse MapStatusHistory(SellingPortfolioStatusHistory entity) => new()
    {
        Id = entity.Id,
        PortfolioId = entity.PortfolioId,
        FromStatus = entity.FromStatus,
        ToStatus = entity.ToStatus,
        ChangedByUserId = entity.ChangedByUserId,
        ChangedAtUtc = entity.ChangedAtUtc,
        Notes = entity.Notes,
    };
}
