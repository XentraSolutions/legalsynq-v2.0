using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using BuildingBlocks.Exceptions;
using BuildingBlocks.Context;
using BuildingBlocks.Notifications;
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
    private readonly IContactRepository _contactRepo;
    private readonly ILienSettlementRepository _settlementRepo;
    private readonly ISettlementPaymentDetailRepository _paymentDetailRepo;
    private readonly IServicingItemRepository _servicingItemRepo;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuditPublisher _audit;
    private readonly INotificationPublisher _notifications;
    private readonly ISellingBuyerAccessLinkService _buyerAccessLinks;
    private readonly ILienEligibilityValidator _eligibilityValidator;
    private readonly ISellerOrganizationDisplayResolver _sellerOrganizationDisplayResolver;
    private readonly ICurrentRequestContext _currentRequestContext;
    private readonly ILogger<SellingPortfolioService> _logger;

    public SellingPortfolioService(
        ISellingPortfolioRepository portfolioRepo,
        ILienRepository lienRepo,
        ICaseRepository caseRepo,
        IContactRepository contactRepo,
        ILienSettlementRepository settlementRepo,
        ISettlementPaymentDetailRepository paymentDetailRepo,
        IServicingItemRepository servicingItemRepo,
        IUnitOfWork unitOfWork,
        IAuditPublisher audit,
        INotificationPublisher notifications,
        ISellingBuyerAccessLinkService buyerAccessLinks,
        ILienEligibilityValidator eligibilityValidator,
        ISellerOrganizationDisplayResolver sellerOrganizationDisplayResolver,
        ICurrentRequestContext currentRequestContext,
        ILogger<SellingPortfolioService> logger)
    {
        _portfolioRepo = portfolioRepo;
        _lienRepo = lienRepo;
        _caseRepo = caseRepo;
        _contactRepo = contactRepo;
        _settlementRepo = settlementRepo;
        _paymentDetailRepo = paymentDetailRepo;
        _servicingItemRepo = servicingItemRepo;
        _unitOfWork = unitOfWork;
        _audit = audit;
        _notifications = notifications;
        _buyerAccessLinks = buyerAccessLinks;
        _eligibilityValidator = eligibilityValidator;
        _sellerOrganizationDisplayResolver = sellerOrganizationDisplayResolver;
        _currentRequestContext = currentRequestContext;
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
            request.Description,
            request.InternalNotes,
            request.TargetGrouping);

        portfolio.AddInitialStatusHistory(actingUserId);

        foreach (var lienId in request.LienIds.Distinct())
        {
            var snapshot = await CreateLienSnapshotAsync(tenantId, sellerOrgId, portfolio.Id, lienId, actingUserId, ct);
            portfolio.AddLien(snapshot, actingUserId);
        }

        foreach (var buyerOrgId in request.BuyerOrgIds.Distinct())
            portfolio.AddBuyer(buyerOrgId, actingUserId);

        await InTransactionAsync(async () =>
        {
            await _portfolioRepo.AddAsync(portfolio, ct);
            await AddActivityAsync(
                portfolio,
                actingUserId,
                "LIEN_SALE_PORTFOLIO_CREATED",
                "SellingPortfolio",
                $"Selling portfolio '{portfolio.PortfolioNumber}' created",
                portfolio.Id.ToString(),
                ct: ct);
        }, ct);

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
        portfolio.Update(request.Name, actingUserId, request.Description, request.InternalNotes, request.TargetGrouping);
        await InTransactionAsync(async () =>
        {
            await _portfolioRepo.UpdateAsync(portfolio, ct);
            await AddActivityAsync(
                portfolio,
                actingUserId,
                "LIEN_SALE_PORTFOLIO_UPDATED",
                "SellingPortfolio",
                $"Selling portfolio '{portfolio.PortfolioNumber}' updated",
                portfolio.Id.ToString(),
                ct: ct);
        }, ct);

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

    public async Task<AddSellingPortfolioLiensResponse> AddLiensAsync(
        Guid tenantId,
        Guid id,
        Guid sellerOrgId,
        Guid actingUserId,
        AddSellingPortfolioLiensRequest request,
        CancellationToken ct = default)
    {
        var requestedLiens = BuildLienAssignmentRequests(request);
        if (requestedLiens.Count == 0)
            throw new ValidationException("One or more required fields are missing or invalid.",
                new Dictionary<string, string[]> { ["liens"] = ["At least one lien id or code is required."] });

        var portfolio = await RequirePortfolioAsync(tenantId, id, ct);
        EnsureSellerPortfolio(portfolio, sellerOrgId);

        var results = new List<AddSellingPortfolioLienResult>();

        foreach (var requestedLien in requestedLiens)
        {
            if (string.IsNullOrWhiteSpace(requestedLien))
            {
                results.Add(FailedLienResult(requestedLien, Guid.Empty, null, "INVALID_LIEN_REFERENCE", "Lien id/code cannot be empty."));
                continue;
            }

            if (portfolio.Status != SellingPortfolioStatus.Draft)
            {
                results.Add(FailedLienResult(
                    requestedLien,
                    TryParseLienId(requestedLien),
                    null,
                    "PORTFOLIO_NOT_EDITABLE",
                    $"Liens can only be added while the portfolio is in '{SellingPortfolioStatus.Draft}'."));
                continue;
            }

            var lien = await ResolveLienForAssignmentAsync(tenantId, requestedLien, ct);
            if (lien is null)
            {
                results.Add(FailedLienResult(requestedLien, TryParseLienId(requestedLien), null, "LIEN_NOT_FOUND", $"Lien '{requestedLien}' was not found."));
                continue;
            }

            var eligibility = await _eligibilityValidator.ValidateAsync(lien, portfolio, ct);
            if (!eligibility.IsEligible)
            {
                LogEligibilityFailure(tenantId, actingUserId, portfolio, lien, eligibility);
                results.Add(FailedLienResult(
                    requestedLien,
                    lien.Id,
                    lien.LienNumber,
                    string.Join(",", eligibility.Violations.Select(v => v.RuleCode)),
                    string.Join(" ", eligibility.Violations.Select(v => v.Message))));
                continue;
            }

            if (lien.SellingOrgId != sellerOrgId && lien.OrgId != sellerOrgId)
            {
                results.Add(FailedLienResult(
                    requestedLien,
                    lien.Id,
                    lien.LienNumber,
                    "SELLER_OWNERSHIP_MISMATCH",
                    $"Lien '{lien.LienNumber}' is not owned by the seller organization."));
                continue;
            }

            string? caseExternalId = null;
            if (lien.CaseId.HasValue)
            {
                var caseEntity = await _caseRepo.GetByIdAsync(tenantId, lien.CaseId.Value, ct);
                caseExternalId = caseEntity?.ExternalReference;
            }

            var snapshot = SellingPortfolioLien.CreateSnapshot(tenantId, portfolio.Id, lien, caseExternalId, actingUserId);
            portfolio.AddLien(snapshot, actingUserId);

            results.Add(new AddSellingPortfolioLienResult
            {
                RequestedLien = requestedLien,
                LienId = lien.Id,
                LienCode = lien.LienNumber,
                Success = true,
                Status = "added",
            });
        }

        if (results.Any(r => r.Success))
        {
            await InTransactionAsync(async () =>
            {
                await _portfolioRepo.UpdateAsync(portfolio, ct);
                await AddActivityAsync(
                    portfolio,
                    actingUserId,
                    "LIENS_ADDED_TO_PORTFOLIO",
                    "SellingPortfolio",
                    $"{results.Count(r => r.Success)} lien(s) assigned to selling portfolio '{portfolio.PortfolioNumber}'",
                    portfolio.Id.ToString(),
                    $"{{\"addedCount\":{results.Count(r => r.Success)},\"failedCount\":{results.Count(r => !r.Success)}}}",
                    ct);
            }, ct);

            var addedLienIds = string.Join(",", results.Where(r => r.Success).Select(r => r.LienId));
            _audit.Publish(
                eventType: "liens.selling_portfolio.liens_added",
                action: "assign_liens",
                description: $"{results.Count(r => r.Success)} lien(s) assigned to selling portfolio '{portfolio.PortfolioNumber}'",
                tenantId: tenantId,
                actorUserId: actingUserId,
                entityType: "SellingPortfolio",
                entityId: portfolio.Id.ToString(),
                metadata: $"{{\"addedLienIds\":\"{addedLienIds}\",\"requestedCount\":{requestedLiens.Count},\"failedCount\":{results.Count(r => !r.Success)}}}");
        }

        return new AddSellingPortfolioLiensResponse
        {
            PortfolioId = portfolio.Id,
            RequestedCount = requestedLiens.Count,
            AddedCount = results.Count(r => r.Success),
            FailedCount = results.Count(r => !r.Success),
            Results = results,
            SuccessfulAssignments = results.Where(r => r.Success).ToList(),
            FailedAssignments = results.Where(r => !r.Success).ToList(),
            Portfolio = MapToResponse(portfolio),
        };
    }

    public async Task<RemoveSellingPortfolioLiensResponse> RemoveLiensAsync(
        Guid tenantId,
        Guid id,
        Guid sellerOrgId,
        Guid actingUserId,
        RemoveSellingPortfolioLiensRequest request,
        CancellationToken ct = default)
    {
        if (request.LienIds.Count == 0)
            throw new ValidationException("One or more required fields are missing or invalid.",
                new Dictionary<string, string[]> { ["lienIds"] = ["At least one lien id is required."] });

        var portfolio = await RequirePortfolioAsync(tenantId, id, ct);
        EnsureSellerPortfolio(portfolio, sellerOrgId);

        var results = new List<RemoveSellingPortfolioLienResult>();
        var removed = new List<SellingPortfolioLien>();

        foreach (var lienId in request.LienIds)
        {
            if (lienId == Guid.Empty)
            {
                results.Add(FailedRemoveLienResult(lienId, null, "INVALID_LIEN_ID", "Lien id cannot be empty."));
                continue;
            }

            if (portfolio.Status != SellingPortfolioStatus.Draft)
            {
                results.Add(FailedRemoveLienResult(
                    lienId,
                    null,
                    "PORTFOLIO_NOT_EDITABLE",
                    $"Liens can only be removed while the portfolio is in '{SellingPortfolioStatus.Draft}'."));
                continue;
            }

            var portfolioLien = portfolio.Liens.FirstOrDefault(l => l.LienId == lienId);
            if (portfolioLien is null)
            {
                results.Add(FailedRemoveLienResult(
                    lienId,
                    null,
                    "LIEN_NOT_IN_PORTFOLIO",
                    $"Lien '{lienId}' is not assigned to this portfolio."));
                continue;
            }

            portfolio.RemoveLien(lienId, actingUserId);
            removed.Add(portfolioLien);

            results.Add(new RemoveSellingPortfolioLienResult
            {
                LienId = portfolioLien.LienId,
                LienCode = portfolioLien.LienNumber,
                Success = true,
                Status = "removed",
            });
        }

        if (removed.Count > 0)
        {
            await InTransactionAsync(async () =>
            {
                await _portfolioRepo.UpdateAsync(portfolio, ct);
                await AddActivityAsync(
                    portfolio,
                    actingUserId,
                    "LIENS_REMOVED_FROM_PORTFOLIO",
                    "SellingPortfolio",
                    $"{removed.Count} lien(s) removed from selling portfolio '{portfolio.PortfolioNumber}'",
                    portfolio.Id.ToString(),
                    $"{{\"removedCount\":{removed.Count},\"failedCount\":{results.Count(r => !r.Success)}}}",
                    ct);
            }, ct);

            foreach (var removedLien in removed)
            {
                _audit.Publish(
                    eventType: "liens.selling_portfolio.lien_removed",
                    action: "LIEN_REMOVED_FROM_PORTFOLIO",
                    description: $"Lien '{removedLien.LienNumber}' removed from selling portfolio '{portfolio.PortfolioNumber}'",
                    tenantId: tenantId,
                    actorUserId: actingUserId,
                    entityType: "SellingPortfolioLien",
                    entityId: removedLien.Id.ToString(),
                    metadata: $"{{\"portfolioId\":\"{portfolio.Id}\",\"lienId\":\"{removedLien.LienId}\",\"lienCode\":\"{removedLien.LienNumber}\"}}");
            }
        }

        return new RemoveSellingPortfolioLiensResponse
        {
            PortfolioId = portfolio.Id,
            RequestedCount = request.LienIds.Count,
            RemovedCount = results.Count(r => r.Success),
            FailedCount = results.Count(r => !r.Success),
            Results = results,
            Portfolio = MapToResponse(portfolio),
        };
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

        await InTransactionAsync(async () =>
        {
            await _portfolioRepo.UpdateAsync(portfolio, ct);
            await AddActivityAsync(
                portfolio,
                actingUserId,
                "BUYERS_ADDED_TO_PORTFOLIO",
                "SellingPortfolio",
                $"{request.BuyerOrgIds.Distinct().Count()} buyer organization(s) added to selling portfolio '{portfolio.PortfolioNumber}'",
                portfolio.Id.ToString(),
                ct: ct);
        }, ct);
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
        var toStatus = portfolio.Status;

        await InTransactionAsync(async () =>
        {
            await _portfolioRepo.UpdateAsync(portfolio, ct);
            await AddActivityAsync(
                portfolio,
                actingUserId,
                ResolveStatusActivityAction(toStatus),
                "SellingPortfolio",
                $"Selling portfolio '{portfolio.PortfolioNumber}' transitioned from {fromStatus} to {toStatus}",
                portfolio.Id.ToString(),
                $"{{\"fromStatus\":\"{fromStatus}\",\"toStatus\":\"{toStatus}\"}}",
                ct);
        }, ct);

        _audit.Publish(
            eventType: "liens.selling_portfolio.status_changed",
            action: "transition",
            description: $"Selling portfolio '{portfolio.PortfolioNumber}' transitioned from {fromStatus} to {toStatus}",
            tenantId: tenantId,
            actorUserId: actingUserId,
            entityType: "SellingPortfolio",
            entityId: portfolio.Id.ToString(),
            metadata: $"{{\"fromStatus\":\"{fromStatus}\",\"toStatus\":\"{toStatus}\"}}");

        return MapToResponse(portfolio);
    }

    public async Task<SellingPortfolioResponse> PublishAsync(
        Guid tenantId,
        Guid id,
        Guid sellerOrgId,
        Guid actingUserId,
        string? notes = null,
        CancellationToken ct = default)
    {
        var portfolio = await RequirePortfolioAsync(tenantId, id, ct);
        EnsureSellerPortfolio(portfolio, sellerOrgId);

        var transitions = new List<(string FromStatus, string ToStatus, string? Notes)>();

        if (portfolio.Status == SellingPortfolioStatus.Draft)
        {
            var fromStatus = portfolio.Status;
            portfolio.TransitionStatus(
                SellingPortfolioStatus.ReadyForReview,
                actingUserId,
                "Ready for review before publishing");
            transitions.Add((fromStatus, portfolio.Status, "Ready for review before publishing"));
        }

        var publishFromStatus = portfolio.Status;
        portfolio.TransitionStatus(SellingPortfolioStatus.Published, actingUserId, notes);
        transitions.Add((publishFromStatus, portfolio.Status, notes));

        await InTransactionAsync(async () =>
        {
            await _portfolioRepo.UpdateAsync(portfolio, ct);

            foreach (var transition in transitions)
            {
                await AddActivityAsync(
                    portfolio,
                    actingUserId,
                    ResolveStatusActivityAction(transition.ToStatus),
                    "SellingPortfolio",
                    $"Selling portfolio '{portfolio.PortfolioNumber}' transitioned from {transition.FromStatus} to {transition.ToStatus}",
                    portfolio.Id.ToString(),
                    $"{{\"fromStatus\":\"{transition.FromStatus}\",\"toStatus\":\"{transition.ToStatus}\"}}",
                    ct);
            }
        }, ct);

        foreach (var transition in transitions)
        {
            _audit.Publish(
                eventType: "liens.selling_portfolio.status_changed",
                action: "transition",
                description: $"Selling portfolio '{portfolio.PortfolioNumber}' transitioned from {transition.FromStatus} to {transition.ToStatus}",
                tenantId: tenantId,
                actorUserId: actingUserId,
                entityType: "SellingPortfolio",
                entityId: portfolio.Id.ToString(),
                metadata: $"{{\"fromStatus\":\"{transition.FromStatus}\",\"toStatus\":\"{transition.ToStatus}\"}}");
        }

        return MapToResponse(portfolio);
    }

    public async Task<SellingPortfolioResponse> WithdrawAsync(
        Guid tenantId,
        Guid id,
        Guid sellerOrgId,
        Guid actingUserId,
        string? notes = null,
        CancellationToken ct = default)
    {
        return await TransitionStatusAsync(
            tenantId,
            id,
            sellerOrgId,
            actingUserId,
            new TransitionSellingPortfolioStatusRequest
            {
                Status = SellingPortfolioStatus.Withdrawn,
                Notes = notes,
            },
            ct);
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

    public async Task<IReadOnlyList<SellingPortfolioActivityResponse>> GetActivityAsync(
        Guid tenantId,
        Guid id,
        Guid sellerOrgId,
        CancellationToken ct = default)
    {
        var portfolio = await RequirePortfolioAsync(tenantId, id, ct);
        EnsureSellerPortfolio(portfolio, sellerOrgId);

        var activity = await _portfolioRepo.GetActivityAsync(tenantId, id, ct);
        return activity.Select(MapActivity).ToList();
    }

    public async Task<SellingPortfolioAnalyticsResponse> GetAnalyticsAsync(
        Guid tenantId,
        Guid id,
        Guid sellerOrgId,
        CancellationToken ct = default)
    {
        var portfolio = await RequirePortfolioAsync(tenantId, id, ct);
        EnsureSellerPortfolio(portfolio, sellerOrgId);

        var lienIds = portfolio.Liens.Select(l => l.LienId).Distinct().ToList();
        var payments = await _paymentDetailRepo.GetByLienIdsAsync(tenantId, lienIds, ct);
        var settlements = await _settlementRepo.GetByLienIdsAsync(tenantId, lienIds, ct);
        var paymentTotal = payments.Sum(p => p.Amount);
        var scheduledSettlementTotal = settlements.Sum(s => s.Amount);
        var balances = portfolio.Liens.Select(l => l.CurrentBalance ?? 0m).ToList();
        var totalOutstanding = balances.Sum();
        var lienCount = portfolio.Liens.Count;
        var activityCount = (await _portfolioRepo.GetActivityAsync(tenantId, id, ct)).Count;
        var settlementExposure = scheduledSettlementTotal > 0m
            ? Math.Max(scheduledSettlementTotal - paymentTotal, 0m)
            : Math.Max(totalOutstanding - paymentTotal, 0m);

        return new SellingPortfolioAnalyticsResponse
        {
            PortfolioId = portfolio.Id,
            Financial = new SellingPortfolioFinancialSummary
            {
                TotalReceivables = portfolio.Liens.Sum(l => l.OriginalAmount),
                TotalOutstandingBalance = totalOutstanding,
                SettlementExposure = settlementExposure,
                PaymentTotal = paymentTotal,
                AverageLienBalance = lienCount == 0 ? 0m : decimal.Round(totalOutstanding / lienCount, 2),
            },
            AgingBuckets = BuildAgingBuckets(portfolio.Liens),
            Operational = new SellingPortfolioOperationalSummary
            {
                LienCount = lienCount,
                Status = portfolio.Status,
                PublishedAtUtc = portfolio.PublishedAtUtc,
                ClosedAtUtc = portfolio.ClosedAtUtc,
                ActivityCount = activityCount,
            },
            Concentrations = BuildConcentrations(portfolio.Liens),
        };
    }

    public async Task<SendLienBuyerEmailResponse> SendBuyerEmailAsync(
        Guid tenantId,
        Guid portfolioId,
        string lienIdOrCode,
        Guid sellerOrgId,
        Guid actingUserId,
        SendLienBuyerEmailRequest request,
        CancellationToken ct = default)
    {
        var errors = new Dictionary<string, string[]>();
        var detailsUrl = request.DetailsUrl.Trim();
        Uri? detailsUri = null;
        if (string.IsNullOrWhiteSpace(lienIdOrCode))
            errors["lienIdOrCode"] = ["Lien ID/code is required."];
        if (request.BuyerContactId == Guid.Empty)
            errors["buyerContactId"] = ["Buyer contact id is required."];
        if (string.IsNullOrWhiteSpace(detailsUrl) ||
            !Uri.TryCreate(detailsUrl, UriKind.Absolute, out detailsUri))
        {
            errors["detailsUrl"] = ["A valid absolute lien details URL is required."];
        }

        if (errors.Count > 0)
            throw new ValidationException("One or more required fields are missing or invalid.", errors);

        var portfolio = await RequirePortfolioAsync(tenantId, portfolioId, ct);
        EnsureSellerPortfolio(portfolio, sellerOrgId);

        var lien = await ResolveLienAsync(tenantId, lienIdOrCode, ct)
            ?? throw new NotFoundException($"Lien '{lienIdOrCode}' not found for tenant '{tenantId}'.");

        if (lien.SellingOrgId != sellerOrgId && lien.OrgId != sellerOrgId)
            throw new ValidationException("Referenced lien is not owned by the seller organization.",
                new Dictionary<string, string[]> { ["lienIdOrCode"] = [$"Lien '{lienIdOrCode}' is not owned by seller organization '{sellerOrgId}'."] });

        var portfolioLien = portfolio.Liens.FirstOrDefault(l =>
            l.LienId == lien.Id ||
            string.Equals(l.LienNumber, lien.LienNumber, StringComparison.OrdinalIgnoreCase));

        if (portfolioLien is null)
            throw new ValidationException("Referenced lien is not part of the selling portfolio.",
                new Dictionary<string, string[]> { ["lienIdOrCode"] = [$"Lien '{lienIdOrCode}' is not attached to selling portfolio '{portfolioId}'."] });

        var contact = await _contactRepo.GetByIdAsync(tenantId, request.BuyerContactId, ct)
            ?? throw new NotFoundException($"Buyer contact '{request.BuyerContactId}' not found for tenant '{tenantId}'.");

        if (!contact.IsActive)
            throw new ValidationException("Buyer contact is inactive.",
                new Dictionary<string, string[]> { ["buyerContactId"] = ["Buyer contact must be active."] });

        if (string.IsNullOrWhiteSpace(contact.Email))
            throw new ValidationException("Buyer contact email is required.",
                new Dictionary<string, string[]> { ["buyerContactId"] = ["Buyer contact must have an email address."] });

        if (!portfolio.Buyers.Any(b => b.BuyerOrgId == contact.OrgId))
            throw new ValidationException("Buyer contact is not associated with this selling portfolio.",
                new Dictionary<string, string[]> { ["buyerContactId"] = [$"Buyer contact '{request.BuyerContactId}' does not belong to a buyer organization on portfolio '{portfolioId}'."] });

        var caseEntity = lien.CaseId.HasValue
            ? await _caseRepo.GetByIdAsync(tenantId, lien.CaseId.Value, ct)
            : null;

        var plaintiffName = BuildPlaintiffName(caseEntity, lien);
        var serviceOrLossDate = (caseEntity?.DateOfIncident ?? lien.IncidentDate)
            ?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
            ?? "Unknown date";
        var lienCode = string.IsNullOrWhiteSpace(lien.LienNumber) ? lien.Id.ToString() : lien.LienNumber;
        var subject = $"{plaintiffName} - {serviceOrLossDate} - {lienCode}";
        var body =
            $"Hi {contact.DisplayName}, please find the lien details at the link below:{Environment.NewLine}{Environment.NewLine}" +
            $"{detailsUri!}{Environment.NewLine}{Environment.NewLine}" +
            "Let me know if you have any questions. Thank you.";

        var notificationResult = await _notifications.SendEmailAsync(
            "liens.selling.buyer_lien_details",
            tenantId,
            contact.Email.Trim(),
            subject,
            body,
            new Dictionary<string, string>
            {
                ["tenantId"] = tenantId.ToString(),
                ["portfolioId"] = portfolio.Id.ToString(),
                ["lienId"] = lien.Id.ToString(),
                ["lienCode"] = lienCode,
                ["buyerContactId"] = contact.Id.ToString(),
                ["buyerOrgId"] = contact.OrgId.ToString(),
                ["requestedBy"] = actingUserId.ToString(),
            },
            ct);

        if (!notificationResult.Succeeded)
        {
            throw new ServiceUnavailableException(
                notificationResult.LastErrorMessage
                ?? $"Buyer lien email was not sent. Notification status: {notificationResult.Status}.");
        }

        _audit.Publish(
            eventType: "liens.selling.buyer_lien_email_sent",
            action: "send_email",
            description: $"Buyer lien details email sent for lien '{lienCode}'",
            tenantId: tenantId,
            actorUserId: actingUserId,
            entityType: "Lien",
            entityId: lien.Id.ToString(),
            metadata: $"{{\"portfolioId\":\"{portfolio.Id}\",\"buyerContactId\":\"{contact.Id}\"}}");

        return new SendLienBuyerEmailResponse
        {
            Success = true,
            NotificationId = notificationResult.NotificationId,
            NotificationStatus = notificationResult.Status,
            LienId = lien.Id,
            LienCode = lienCode,
            BuyerContactId = contact.Id,
            BuyerOrgId = contact.OrgId,
            BuyerName = contact.DisplayName,
            BuyerEmail = contact.Email.Trim(),
            Subject = subject,
            Body = body,
        };
    }

    public async Task<ConfirmSellingLienSaleResponse> ConfirmSaleAsync(
        Guid tenantId,
        Guid lienId,
        Guid sellerOrgId,
        Guid actingUserId,
        ConfirmSellingLienSaleRequest request,
        CancellationToken ct = default)
    {
        if (!request.ConfirmationAccepted)
        {
            throw new ValidationException("Sale confirmation must be accepted.",
                new Dictionary<string, string[]>
                {
                    ["confirmationAccepted"] = ["Confirm the sale before submitting it."],
                });
        }

        var lien = await _lienRepo.GetByIdAsync(tenantId, lienId, ct)
            ?? throw new NotFoundException($"Lien '{lienId}' not found for tenant '{tenantId}'.");

        if (lien.SellingOrgId != sellerOrgId && lien.OrgId != sellerOrgId)
            throw new ValidationException("Referenced lien is not owned by the seller organization.",
                new Dictionary<string, string[]> { ["lienId"] = [$"Lien '{lienId}' is not owned by seller organization '{sellerOrgId}'."] });

        if (lien.Status != LienStatus.Draft &&
            !(lien.Status == LienStatus.Offered &&
              string.Equals(lien.SellerStatus, SellingLienStatus.SubmittedForSale, StringComparison.Ordinal)))
        {
            throw new ValidationException("Lien cannot be confirmed for sale from its current status.",
                new Dictionary<string, string[]>
                {
                    ["status"] = [$"Only draft or already submitted-for-sale liens can be confirmed. Current status: '{lien.Status}'."],
                });
        }

        if (!lien.AskAmount.HasValue || lien.AskAmount.Value <= 0m)
        {
            throw new ValidationException("Ask amount is required before confirming sale.",
                new Dictionary<string, string[]>
                {
                    ["askAmount"] = ["A positive AskAmount is required before confirming sale."],
                });
        }

        var notificationContext = await BuildConfirmSaleNotificationContextAsync(
            tenantId,
            sellerOrgId,
            actingUserId,
            lien,
            ct);

        var buyerNotificationIdempotencyKey = BuildConfirmSaleNotificationIdempotencyKey(
            tenantId,
            lien.Id,
            notificationContext.BuyerContact.Id);
        var sellerNotificationIdempotencyKey = BuildConfirmSaleSellerNotificationIdempotencyKey(
            tenantId,
            lien.Id,
            notificationContext.SellerContact.Id,
            notificationContext.BuyerContact.Id);

        SellingBuyerAccessLinkResult? buyerAccessLink = null;
        SellingBuyerAccessLinkResult? sellerAccessLink = null;
        await InTransactionAsync(async () =>
        {
            if (lien.Status == LienStatus.Draft)
                lien.ListForSale(lien.AskAmount.Value, actingUserId);

            await _lienRepo.UpdateAsync(lien, ct);

            buyerAccessLink = await _buyerAccessLinks.CreateOrGetForConfirmSaleAsync(
                tenantId,
                lien.Id,
                sellerOrgId,
                notificationContext.BuyerContact.OrgId,
                notificationContext.BuyerContact.Id,
                actingUserId,
                buyerNotificationIdempotencyKey,
                TimeSpan.FromDays(30),
                ct);

            sellerAccessLink = await _buyerAccessLinks.CreateOrGetForConfirmSaleSellerViewAsync(
                tenantId,
                lien.Id,
                sellerOrgId,
                notificationContext.BuyerContact.OrgId,
                notificationContext.BuyerContact.Id,
                actingUserId,
                sellerNotificationIdempotencyKey,
                TimeSpan.FromDays(30),
                ct);
        }, ct);

        ConfirmSellingLienBuyerNotificationResponse? notification = null;
        ConfirmSellingLienSellerNotificationResponse? sellerNotification = null;
        if (buyerAccessLink is not null && sellerAccessLink is not null)
        {
            notification = await SendConfirmSaleNotificationAsync(
                tenantId,
                actingUserId,
                lien,
                notificationContext,
                buyerAccessLink,
                buyerNotificationIdempotencyKey,
                ct);

            sellerNotification = await SendConfirmSaleSellerNotificationAsync(
                tenantId,
                actingUserId,
                lien,
                notificationContext,
                sellerAccessLink,
                buyerAccessLink,
                sellerNotificationIdempotencyKey,
                ct);
        }

        _audit.Publish(
            eventType: "liens.selling.confirm_sale",
            action: "confirm_sale",
            description: $"Lien '{lien.LienNumber}' confirmed for sale",
            tenantId: tenantId,
            actorUserId: actingUserId,
            entityType: "Lien",
            entityId: lien.Id.ToString(),
            metadata: "{\"buyerNotificationRequired\":true,\"sellerNotificationRequired\":true}");

        return MapConfirmSaleResponse(lien, notification, sellerNotification);
    }

    private async Task<ConfirmSaleNotificationContext> BuildConfirmSaleNotificationContextAsync(
        Guid tenantId,
        Guid sellerOrgId,
        Guid actingUserId,
        Lien lien,
        CancellationToken ct)
    {
        var errors = new Dictionary<string, string[]>();

        if (!lien.FundingCompanyId.HasValue || lien.FundingCompanyId.Value == Guid.Empty)
            errors["fundingCompanyId"] = ["FundingCompanyId is required before sending the buyer notification."];

        if (!lien.FundingCompanyContactId.HasValue || lien.FundingCompanyContactId.Value == Guid.Empty)
            errors["fundingCompanyContactId"] = ["FundingCompanyContactId is required before sending the buyer notification."];

        if (!lien.InitialServiceDate.HasValue)
            errors["initialServiceDate"] = ["InitialServiceDate is required before sending the buyer notification."];

        if (errors.Count > 0)
            throw new ValidationException("One or more required fields are missing or invalid.", errors);

        var buyerContact = await _contactRepo.GetByIdAsync(tenantId, lien.FundingCompanyContactId!.Value, ct)
            ?? throw new NotFoundException($"Buyer contact '{lien.FundingCompanyContactId.Value}' not found for tenant '{tenantId}'.");

        if (!buyerContact.IsActive)
            errors["fundingCompanyContactId"] = ["Buyer contact must be active."];

        if (buyerContact.OrgId != lien.FundingCompanyId!.Value)
            errors["fundingCompanyContactId"] = ["Buyer contact must belong to the selected funding company."];

        if (string.IsNullOrWhiteSpace(buyerContact.Email))
            errors["fundingCompanyContactId"] = ["Buyer contact must have an email address."];

        var caseEntity = lien.CaseId.HasValue
            ? await _caseRepo.GetByIdAsync(tenantId, lien.CaseId.Value, ct)
            : null;
        var handlingLawFirmContact = await ResolveHandlingLawFirmContactAsync(tenantId, caseEntity, ct);
        var handlingLawFirm = ResolveHandlingLawFirmName(handlingLawFirmContact);
        if (string.IsNullOrWhiteSpace(handlingLawFirm))
            errors["handlingLawFirm"] = ["A real handling law firm is required before sending the buyer notification."];

        var sellerContacts = await _contactRepo.GetByOrgIdAsync(tenantId, sellerOrgId, isActive: true, ct);
        var sellerContact = SelectSellerContact(sellerContacts, handlingLawFirmContact?.Id)
            ?? SelectSellerContact(sellerContacts);
        var sellerDisplay = await _sellerOrganizationDisplayResolver.ResolveAsync(
            tenantId,
            sellerOrgId,
            sellerContacts,
            sellerUserId: actingUserId,
            fallbackEmail: _currentRequestContext.Email,
            ct: ct);
        var sellerEmail = FirstNonEmpty(sellerDisplay.Email);
        if (sellerContact is null)
            errors["sellerContact"] = ["An active seller contact is required before sending the buyer notification."];
        else
        {
            if (string.IsNullOrWhiteSpace(sellerEmail))
                errors["sellerEmail"] = ["A seller email address is required before sending the buyer notification."];
        }

        if (errors.Count > 0)
            throw new ValidationException("One or more required fields are missing or invalid.", errors);

        var caseManager = await ResolveCaseManagerAsync(tenantId, caseEntity, ct);
        var documents = await GetSupportingDocumentsAsync(tenantId, lien, ct);

        return new ConfirmSaleNotificationContext(
            buyerContact,
            sellerContact!,
            sellerDisplay,
            sellerEmail!,
            caseEntity,
            handlingLawFirmContact!,
            caseManager,
            documents);
    }

    private async Task<ConfirmSellingLienBuyerNotificationResponse> SendConfirmSaleNotificationAsync(
        Guid tenantId,
        Guid actingUserId,
        Lien lien,
        ConfirmSaleNotificationContext context,
        SellingBuyerAccessLinkResult accessLink,
        string idempotencyKey,
        CancellationToken ct)
    {
        if (IsSubmittedNotificationStatus(accessLink.NotificationStatus))
        {
            return new ConfirmSellingLienBuyerNotificationResponse
            {
                Requested = true,
                Submitted = true,
                NotificationId = accessLink.NotificationId,
                NotificationStatus = accessLink.NotificationStatus,
                BuyerAccessLinkId = accessLink.Id,
                BuyerPortalUrl = accessLink.BuyerPortalUrl,
                ExpiresAtUtc = accessLink.ExpiresAtUtc,
                BuyerContactId = context.BuyerContact.Id,
                BuyerOrgId = context.BuyerContact.OrgId,
                BuyerEmail = context.BuyerContact.Email!.Trim(),
            };
        }

        var email = BuildConfirmSaleEmail(lien, context, accessLink);
        var metadata = new Dictionary<string, string>
        {
            ["tenantId"] = tenantId.ToString(),
            ["lienId"] = lien.Id.ToString(),
            ["lienCode"] = ResolveLienCode(lien),
            ["buyerContactId"] = context.BuyerContact.Id.ToString(),
            ["buyerOrgId"] = context.BuyerContact.OrgId.ToString(),
            ["sellerOrgId"] = context.SellerContact.OrgId.ToString(),
            ["buyerAccessLinkId"] = accessLink.Id.ToString(),
            ["buyerAccessExpiresAtUtc"] = accessLink.ExpiresAtUtc.ToString("O", CultureInfo.InvariantCulture),
            ["requestedBy"] = actingUserId.ToString(),
        };

        try
        {
            var notificationResult = await _notifications.SendEmailAsync(
                NotificationTaxonomy.Liens.Events.SellingLienSubmitted,
                tenantId,
                context.BuyerContact.Email!.Trim(),
                email.Subject,
                email.TextBody,
                metadata,
                ct,
                new NotificationEmailSendOptions(
                    IdempotencyKey: idempotencyKey,
                    TemplateKey: NotificationTaxonomy.Liens.Templates.SellingLienSubmittedEmail,
                    TemplateData: email.TemplateData,
                    RequestedBy: actingUserId.ToString(),
                    BrandedRendering: true,
                    HtmlBody: email.HtmlBody,
                    TextBody: email.TextBody,
                    InlineAttachments: email.InlineAttachments,
                    DisableClickTracking: true));

            await _buyerAccessLinks.MarkNotificationSubmittedAsync(
                tenantId,
                accessLink.Id,
                notificationResult.NotificationId,
                notificationResult.Status,
                ct);

            var submitted = IsSubmittedNotificationStatus(notificationResult.Status) &&
                            !notificationResult.BlockedByPolicy &&
                            string.IsNullOrWhiteSpace(notificationResult.FailureCategory);

            return new ConfirmSellingLienBuyerNotificationResponse
            {
                Requested = true,
                Submitted = submitted,
                NotificationId = notificationResult.NotificationId,
                NotificationStatus = notificationResult.Status,
                FailureMessage = submitted ? null : notificationResult.LastErrorMessage,
                BuyerAccessLinkId = accessLink.Id,
                BuyerPortalUrl = accessLink.BuyerPortalUrl,
                ExpiresAtUtc = accessLink.ExpiresAtUtc,
                BuyerContactId = context.BuyerContact.Id,
                BuyerOrgId = context.BuyerContact.OrgId,
                BuyerEmail = context.BuyerContact.Email.Trim(),
            };
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex,
                "Confirm-sale buyer notification failed: Tenant={TenantId} Lien={LienId} BuyerContact={BuyerContactId}",
                tenantId, lien.Id, context.BuyerContact.Id);

            await _buyerAccessLinks.MarkNotificationSubmittedAsync(
                tenantId,
                accessLink.Id,
                null,
                "failed",
                ct);

            return new ConfirmSellingLienBuyerNotificationResponse
            {
                Requested = true,
                Submitted = false,
                NotificationStatus = "failed",
                FailureMessage = ex.Message,
                BuyerAccessLinkId = accessLink.Id,
                BuyerPortalUrl = accessLink.BuyerPortalUrl,
                ExpiresAtUtc = accessLink.ExpiresAtUtc,
                BuyerContactId = context.BuyerContact.Id,
                BuyerOrgId = context.BuyerContact.OrgId,
                BuyerEmail = context.BuyerContact.Email!.Trim(),
            };
        }
    }

    private async Task<ConfirmSellingLienSellerNotificationResponse> SendConfirmSaleSellerNotificationAsync(
        Guid tenantId,
        Guid actingUserId,
        Lien lien,
        ConfirmSaleNotificationContext context,
        SellingBuyerAccessLinkResult sellerAccessLink,
        SellingBuyerAccessLinkResult buyerAccessLink,
        string idempotencyKey,
        CancellationToken ct)
    {
        if (IsSubmittedNotificationStatus(sellerAccessLink.NotificationStatus))
        {
            return new ConfirmSellingLienSellerNotificationResponse
            {
                Requested = true,
                Submitted = true,
                NotificationId = sellerAccessLink.NotificationId,
                NotificationStatus = sellerAccessLink.NotificationStatus,
                SellerAccessLinkId = sellerAccessLink.Id,
                SellerPortalUrl = sellerAccessLink.PublicPortalUrl,
                ExpiresAtUtc = sellerAccessLink.ExpiresAtUtc,
                SellerContactId = context.SellerContact.Id,
                SellerOrgId = context.SellerContact.OrgId,
                SellerEmail = context.SellerEmail,
            };
        }

        var email = BuildConfirmSaleSellerEmail(lien, context, sellerAccessLink);
        var metadata = new Dictionary<string, string>
        {
            ["tenantId"] = tenantId.ToString(),
            ["lienId"] = lien.Id.ToString(),
            ["lienCode"] = ResolveLienCode(lien),
            ["buyerContactId"] = context.BuyerContact.Id.ToString(),
            ["buyerOrgId"] = context.BuyerContact.OrgId.ToString(),
            ["sellerContactId"] = context.SellerContact.Id.ToString(),
            ["sellerOrgId"] = context.SellerContact.OrgId.ToString(),
            ["buyerAccessLinkId"] = buyerAccessLink.Id.ToString(),
            ["sellerAccessLinkId"] = sellerAccessLink.Id.ToString(),
            ["sellerAccessExpiresAtUtc"] = sellerAccessLink.ExpiresAtUtc.ToString("O", CultureInfo.InvariantCulture),
            ["requestedBy"] = actingUserId.ToString(),
            ["audience"] = "seller",
        };

        try
        {
            var notificationResult = await _notifications.SendEmailAsync(
                NotificationTaxonomy.Liens.Events.SellingLienSubmitted,
                tenantId,
                context.SellerEmail,
                email.Subject,
                email.TextBody,
                metadata,
                ct,
                new NotificationEmailSendOptions(
                    IdempotencyKey: idempotencyKey,
                    TemplateKey: NotificationTaxonomy.Liens.Templates.SellingLienSubmittedEmail,
                    TemplateData: email.TemplateData,
                    RequestedBy: actingUserId.ToString(),
                    BrandedRendering: true,
                    HtmlBody: email.HtmlBody,
                    TextBody: email.TextBody,
                    InlineAttachments: email.InlineAttachments,
                    DisableClickTracking: true));

            await _buyerAccessLinks.MarkNotificationSubmittedAsync(
                tenantId,
                sellerAccessLink.Id,
                notificationResult.NotificationId,
                notificationResult.Status,
                ct);

            var submitted = IsSubmittedNotificationStatus(notificationResult.Status) &&
                            !notificationResult.BlockedByPolicy &&
                            string.IsNullOrWhiteSpace(notificationResult.FailureCategory);

            return new ConfirmSellingLienSellerNotificationResponse
            {
                Requested = true,
                Submitted = submitted,
                NotificationId = notificationResult.NotificationId,
                NotificationStatus = notificationResult.Status,
                FailureMessage = submitted ? null : notificationResult.LastErrorMessage,
                SellerAccessLinkId = sellerAccessLink.Id,
                SellerPortalUrl = sellerAccessLink.PublicPortalUrl,
                ExpiresAtUtc = sellerAccessLink.ExpiresAtUtc,
                SellerContactId = context.SellerContact.Id,
                SellerOrgId = context.SellerContact.OrgId,
                SellerEmail = context.SellerEmail,
            };
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex,
                "Confirm-sale seller notification failed: Tenant={TenantId} Lien={LienId} SellerContact={SellerContactId}",
                tenantId, lien.Id, context.SellerContact.Id);

            await _buyerAccessLinks.MarkNotificationSubmittedAsync(
                tenantId,
                sellerAccessLink.Id,
                null,
                "failed",
                ct);

            return new ConfirmSellingLienSellerNotificationResponse
            {
                Requested = true,
                Submitted = false,
                NotificationStatus = "failed",
                FailureMessage = ex.Message,
                SellerAccessLinkId = sellerAccessLink.Id,
                SellerPortalUrl = sellerAccessLink.PublicPortalUrl,
                ExpiresAtUtc = sellerAccessLink.ExpiresAtUtc,
                SellerContactId = context.SellerContact.Id,
                SellerOrgId = context.SellerContact.OrgId,
                SellerEmail = context.SellerEmail,
            };
        }
    }

    private static ConfirmSaleEmail BuildConfirmSaleEmail(
        Lien lien,
        ConfirmSaleNotificationContext context,
        SellingBuyerAccessLinkResult accessLink)
        => BuildConfirmSaleEmail(lien, context, accessLink, ConfirmSaleEmailAudience.Buyer);

    private static ConfirmSaleEmail BuildConfirmSaleSellerEmail(
        Lien lien,
        ConfirmSaleNotificationContext context,
        SellingBuyerAccessLinkResult accessLink)
        => BuildConfirmSaleEmail(lien, context, accessLink, ConfirmSaleEmailAudience.Seller);

    private static ConfirmSaleEmail BuildConfirmSaleEmail(
        Lien lien,
        ConfirmSaleNotificationContext context,
        SellingBuyerAccessLinkResult accessLink,
        ConfirmSaleEmailAudience audience)
    {
        const string subject = "New Lien Offer";
        var isSellerView = audience == ConfirmSaleEmailAudience.Seller;
        var lienCode = ResolveLienCode(lien);
        var billingAmount = lien.OriginalAmount.ToString("C", CultureInfo.GetCultureInfo("en-US"));
        var initialServiceDate = lien.InitialServiceDate!.Value.ToString("MM/dd/yyyy", CultureInfo.InvariantCulture);
        var sellerName = context.SellerDisplay.Name.Trim();
        var sellerCompany = context.SellerDisplay.Company.Trim();
        var sellerEmail = context.SellerEmail.Trim();
        var buyerName = context.BuyerContact.DisplayName.Trim();
        var buyerCompany = context.BuyerContact.Organization?.Trim();
        var buyerEmail = context.BuyerContact.Email!.Trim();
        var buyerPhone = context.BuyerContact.Phone?.Trim();
        var handlingLawFirm = ResolveHandlingLawFirmName(context.HandlingLawFirmContact)!.Trim();
        var handlingLawFirmContactName = ResolveContactPersonName(context.HandlingLawFirmContact);
        var handlingLawFirmContactEmail = context.HandlingLawFirmContact.Email?.Trim() ?? string.Empty;
        var caseManager = context.CaseManager?.Trim();
        var documents = context.Documents
            .Where(document => !string.IsNullOrWhiteSpace(document.FileName))
            .Select(document => document with
            {
                FileName = document.FileName.Trim(),
                Category = document.Category?.Trim(),
            })
            .ToList();
        var status = isSellerView ? FormatStatusLabel(lien.Status) : "Awaiting Your Response";
        var intro = isSellerView
            ? "A medical lien has been sent to the funding company for review. Review the buyer and asset details below."
            : "A medical lien has been submitted to your company for review and potential purchase. Review the asset overview below to proceed.";
        var informationSectionTitle = isSellerView ? "Buyer Information" : "Seller Information";
        var contactPerson = handlingLawFirmContactName;
        var contactEmail = handlingLawFirmContactEmail;
        var ctaLabel = isSellerView ? "View Lien Details" : "View Lien for Sale";
        var footerCompany = isSellerView
            ? FirstNonEmpty(buyerCompany, buyerName, "the funding company")!
            : sellerCompany;
        var footerEmail = isSellerView ? buyerEmail : sellerEmail;

        var templateData = new Dictionary<string, string>
        {
            ["subject"] = subject,
            ["audience"] = isSellerView ? "seller" : "buyer",
            ["status"] = status,
            ["intro"] = intro,
            ["sellerName"] = sellerName,
            ["sellerCompany"] = sellerCompany,
            ["sellerEmail"] = sellerEmail,
            ["buyerName"] = buyerName,
            ["buyerEmail"] = buyerEmail,
            ["billingAmount"] = billingAmount,
            ["initialServiceDate"] = initialServiceDate,
            ["contactPerson"] = contactPerson,
            ["emailAddress"] = contactEmail,
            ["handlingLawFirm"] = handlingLawFirm,
            ["lienCode"] = lienCode,
            ["buyerPortalUrl"] = accessLink.BuyerPortalUrl,
            ["publicPortalUrl"] = accessLink.PublicPortalUrl,
            ["expiresAtUtc"] = accessLink.ExpiresAtUtc.ToString("O", CultureInfo.InvariantCulture),
        };

        if (!string.IsNullOrWhiteSpace(buyerCompany))
            templateData["buyerCompany"] = buyerCompany;
        if (!isSellerView && !string.IsNullOrWhiteSpace(buyerPhone))
            templateData["buyerPhone"] = buyerPhone;
        if (!string.IsNullOrWhiteSpace(caseManager))
            templateData["caseManager"] = caseManager;

        if (documents.Count > 0)
            templateData["supportingDocuments"] = string.Join(", ", documents.Select(document => document.FileName));

        var sellerRows = new (string Label, string? Value)[]
        {
            ("Seller Name", sellerName),
            ("Seller Company", sellerCompany),
        };

        var buyerRows = new (string Label, string? Value)[]
        {
            ("Buyer Name", buyerName),
            ("Funding Company", buyerCompany),
        };

        var informationRows = isSellerView ? buyerRows : sellerRows;
        var assetRows = new (string Label, string? Value)[]
        {
            ("Billing Amount", billingAmount),
            ("Initial Service Date", initialServiceDate),
            ("Contact Person", contactPerson),
            ("Email Address", contactEmail),
            ("Handling Law Firm", handlingLawFirm),
            ("Case Manager", caseManager),
        };

        var htmlBody = new StringBuilder();
        htmlBody.AppendLine("<!doctype html>");
        htmlBody.AppendLine("<html lang=\"en\">");
        htmlBody.AppendLine("<head>");
        htmlBody.AppendLine("<meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width,initial-scale=1\">");
        htmlBody.AppendLine("<meta name=\"color-scheme\" content=\"light only\"><meta name=\"supported-color-schemes\" content=\"light only\">");
        htmlBody.AppendLine("<title>New Lien Offer</title>");
        htmlBody.AppendLine("<style>");
        htmlBody.AppendLine("@import url('https://fonts.googleapis.com/css2?family=Plus+Jakarta+Sans:wght@400;500;600;700;800&display=swap');");
        htmlBody.AppendLine(":root{color-scheme:light only;supported-color-schemes:light only;}");
        htmlBody.AppendLine("body,table,td,p,a,span{color-scheme:light only;supported-color-schemes:light only;}");
        htmlBody.AppendLine("body,table,td,p,a,span,h1,h2,strong{font-family:'Plus Jakarta Sans',Arial,'Helvetica Neue',Helvetica,sans-serif !important;}");
        htmlBody.AppendLine(".email-bg{background-color:#f4f5f7 !important;}.email-shell{background-color:#ffffff !important;}.email-card{background-color:#ffffff !important;color:#111827 !important;}");
        htmlBody.AppendLine(".email-label{color:#6f6f6f !important;}.email-value{color:#111111 !important;}.email-rule{border-color:#e5e5e5 !important;}");
        htmlBody.AppendLine("@media (prefers-color-scheme: dark){.email-bg{background-color:#f4f5f7 !important;}.email-shell,.email-card{background-color:#ffffff !important;color:#111827 !important;}.email-label{color:#6f6f6f !important;}.email-value{color:#111111 !important;}.email-rule{border-color:#e5e5e5 !important;}}");
        htmlBody.AppendLine("[data-ogsc] .email-bg{background-color:#f4f5f7 !important;}[data-ogsc] .email-shell,[data-ogsc] .email-card{background-color:#ffffff !important;color:#111827 !important;}");
        htmlBody.AppendLine("</style>");
        htmlBody.AppendLine("</head>");
        htmlBody.AppendLine("<body class=\"email-bg\" bgcolor=\"#f4f5f7\" style=\"margin:0;padding:0;background-color:#f4f5f7 !important;font-family:'Plus Jakarta Sans',Arial,'Helvetica Neue',Helvetica,sans-serif;color:#111827 !important;color-scheme:light only;supported-color-schemes:light only;-webkit-text-size-adjust:100%;-ms-text-size-adjust:100%;\">");
        htmlBody.AppendLine("<table role=\"presentation\" width=\"100%\" cellspacing=\"0\" cellpadding=\"0\" bgcolor=\"#f4f5f7\" class=\"email-bg\" style=\"width:100%;border-collapse:collapse;background-color:#f4f5f7 !important;\">");
        htmlBody.AppendLine("<tr><td align=\"center\" bgcolor=\"#f4f5f7\" class=\"email-bg\" style=\"padding:28px 14px;background-color:#f4f5f7 !important;\">");
        htmlBody.AppendLine("<table role=\"presentation\" width=\"560\" cellspacing=\"0\" cellpadding=\"0\" class=\"email-shell\" bgcolor=\"#ffffff\" style=\"width:100%;max-width:560px;border-collapse:separate;border-spacing:0;background-color:#ffffff !important;border-radius:10px;overflow:hidden;\">");
        htmlBody.AppendLine("<tr><td bgcolor=\"#071b31\" style=\"background-color:#071b31 !important;border-radius:10px 10px 0 0;padding:28px 30px 28px;\">");
        htmlBody.AppendLine("<table role=\"presentation\" width=\"100%\" cellspacing=\"0\" cellpadding=\"0\" style=\"width:100%;border-collapse:collapse;margin:0 0 28px 0;\"><tr>");
        htmlBody.AppendLine("<td align=\"left\" style=\"vertical-align:middle;padding:0;\">");
        AppendLegalSynqEmailBrand(htmlBody);
        htmlBody.AppendLine("</td>");
        htmlBody.AppendLine("<td align=\"right\" style=\"vertical-align:middle;padding:0;\">");
        htmlBody.Append("<span style=\"display:inline-block;background-color:#263127 !important;color:#f3c400 !important;border-radius:999px;padding:6px 12px;font-size:12px;font-weight:600;line-height:1.1;white-space:nowrap;\">")
            .Append(Html(status))
            .AppendLine("</span>");
        htmlBody.AppendLine("</td>");
        htmlBody.AppendLine("</tr></table>");
        htmlBody.AppendLine("<h1 style=\"margin:0 0 10px 0;color:#ffffff !important;font-size:24px;line-height:1.25;font-weight:700;letter-spacing:0;\">New Lien Offer</h1>");
        htmlBody.Append("<p style=\"margin:0;color:#ffffff !important;font-size:16px;line-height:1.55;font-weight:400;opacity:.92;\">")
            .Append(Html(intro))
            .AppendLine("</p>");
        htmlBody.AppendLine("</td></tr>");
        htmlBody.AppendLine("<tr><td bgcolor=\"#ffffff\" class=\"email-card\" style=\"background-color:#ffffff !important;color:#111827 !important;border:1px solid #e5e5e5;border-top:0;border-radius:0 0 10px 10px;padding:24px 24px 28px;\">");
        AppendEmailSection(htmlBody, informationSectionTitle, informationRows);
        AppendEmailSection(htmlBody, "Asset Overview", assetRows);

        if (documents.Count > 0)
            AppendDocumentsSection(htmlBody, documents);

        htmlBody.AppendLine("<table role=\"presentation\" width=\"100%\" cellspacing=\"0\" cellpadding=\"0\" style=\"border-collapse:separate;border-spacing:0;margin:4px 0 12px 0;\"><tr>");
        htmlBody.Append("<td align=\"center\" bgcolor=\"#f26a2e\" style=\"background-color:#f26a2e !important;border-radius:8px;\"><a href=\"")
            .Append(Html(accessLink.PublicPortalUrl))
            .Append("\" style=\"display:block;padding:12px 20px;color:#ffffff !important;text-decoration:none;font-size:13px;font-weight:700;line-height:1.1;\">")
            .Append(Html(ctaLabel))
            .AppendLine("</a></td>");
        htmlBody.AppendLine("</tr></table>");
        htmlBody.AppendLine("<p class=\"email-label\" style=\"margin:0 0 20px 0;text-align:center;color:#7a7a7a !important;font-size:13px;line-height:1.5;\">This Link Expires in 30 Days</p>");
        htmlBody.AppendLine("<table role=\"presentation\" width=\"100%\" cellspacing=\"0\" cellpadding=\"0\" bgcolor=\"#ffffff\" class=\"email-card email-rule\" style=\"border-collapse:separate;border-spacing:0;background-color:#ffffff !important;\"><tr>");
        htmlBody.Append("<td style=\"width:28px;padding:15px 0 15px 14px;vertical-align:top;")
            .Append(EmailTableCellBorder(isFirstRow: true, isLastRow: true, leftEdge: true, rightEdge: false))
            .AppendLine("\"><span style=\"display:inline-block;width:14px;height:14px;line-height:14px;text-align:center;border-radius:50%;border:1px solid #f3c400;color:#f3a800 !important;font-size:10px;font-weight:700;\">i</span></td>");
        htmlBody.Append("<td class=\"email-label\" style=\"padding:14px 16px 14px 8px;color:#6f6f6f !important;font-size:13px;line-height:1.55;")
            .Append(EmailTableCellBorder(isFirstRow: true, isLastRow: true, leftEdge: false, rightEdge: true))
            .Append(isSellerView ? "\">This offer was sent to <strong class=\"email-value\" style=\"color:#111111 !important;font-weight:700;\">" : "\">This offer was sent on behalf of the <strong class=\"email-value\" style=\"color:#111111 !important;font-weight:700;\">")
            .Append(Html(footerCompany))
            .Append(isSellerView ? "</strong>. Please contact <a href=\"mailto:" : "</strong>. Please reply directly to <a href=\"mailto:")
            .Append(Html(footerEmail))
            .Append("\" style=\"color:#f26a2e !important;text-decoration:underline;\">")
            .Append(Html(footerEmail))
            .AppendLine("</a> for any questions.</td>");
        htmlBody.AppendLine("</tr></table>");
        htmlBody.AppendLine("</td></tr>");
        htmlBody.AppendLine("</table>");
        htmlBody.AppendLine("</td></tr>");
        htmlBody.AppendLine("</table>");
        htmlBody.AppendLine("</body></html>");

        var textBody = BuildConfirmSaleTextBody(
            status,
            intro,
            accessLink.PublicPortalUrl,
            ctaLabel,
            footerCompany,
            footerEmail,
            isSellerView,
            informationSectionTitle,
            informationRows,
            assetRows,
            documents);

        return new ConfirmSaleEmail(subject, htmlBody.ToString(), textBody, templateData, ConfirmSaleEmailAssets.InlineAttachments);
    }

    private static void AppendEmailSection(
        StringBuilder body,
        string title,
        IReadOnlyList<(string Label, string? Value)> rows)
    {
        var visibleRows = rows
            .Where(row => !string.IsNullOrWhiteSpace(row.Value))
            .ToList();
        if (visibleRows.Count == 0)
            return;

        body.AppendLine("<table role=\"presentation\" width=\"100%\" cellspacing=\"0\" cellpadding=\"0\" bgcolor=\"#ffffff\" class=\"email-card\" style=\"width:100%;border-collapse:collapse;margin:0 0 28px 0;background-color:#ffffff !important;\">");
        AppendEmailSectionHeading(body, title);
        body.AppendLine("<tr><td bgcolor=\"#ffffff\" class=\"email-card\" style=\"padding:0;background-color:#ffffff !important;\">");
        body.AppendLine("<table role=\"presentation\" width=\"100%\" cellspacing=\"0\" cellpadding=\"0\" bgcolor=\"#ffffff\" class=\"email-card email-rule\" style=\"width:100%;border-collapse:separate;border-spacing:0;background-color:#ffffff !important;\">");

        for (var i = 0; i < visibleRows.Count; i++)
        {
            var (label, value) = visibleRows[i];
            var isFirstRow = i == 0;
            var isLastRow = i == visibleRows.Count - 1;
            var labelBorder = EmailTableCellBorder(isFirstRow, isLastRow, leftEdge: true, rightEdge: false);
            var valueBorder = EmailTableCellBorder(isFirstRow, isLastRow, leftEdge: false, rightEdge: true);

            body.Append("<tr><td bgcolor=\"#ffffff\" class=\"email-card email-label\" style=\"width:44%;padding:15px 14px;color:#6f6f6f !important;background-color:#ffffff !important;font-size:13px;line-height:1.45;")
                .Append(labelBorder)
                .Append("\">")
                .Append(Html(label))
                .Append("</td><td align=\"right\" bgcolor=\"#ffffff\" class=\"email-card email-value\" style=\"width:56%;padding:15px 14px;color:#111111 !important;background-color:#ffffff !important;font-size:15px;line-height:1.45;font-weight:500;")
                .Append(valueBorder)
                .Append("\">");
            AppendEmailValue(body, label, value!.Trim());
            body.AppendLine("</td></tr>");
        }

        body.AppendLine("</table>");
        body.AppendLine("</td></tr>");
        body.AppendLine("</table>");
    }

    private static void AppendEmailValue(StringBuilder body, string label, string value)
    {
        if (string.Equals(label, "Email Address", StringComparison.OrdinalIgnoreCase))
        {
            body.Append("<a href=\"mailto:")
                .Append(Html(value))
                .Append("\" style=\"color:#111111 !important;text-decoration:none;\">")
                .Append(Html(value))
                .Append("</a>");
            return;
        }

        body.Append(Html(value));
    }

    private static void AppendLegalSynqEmailBrand(StringBuilder body)
    {
        body.Append("<table role=\"presentation\" cellspacing=\"0\" cellpadding=\"0\" aria-label=\"LegalSynq\" style=\"border-collapse:collapse;\"><tr><td width=\"36\" style=\"width:36px;padding:0 6px 0 0;vertical-align:middle;\"><img src=\"cid:")
            .Append(ConfirmSaleEmailAssets.LegalSynqBrandIconContentId)
            .AppendLine("\" width=\"36\" height=\"36\" alt=\"\" role=\"presentation\" style=\"display:block;width:36px;height:36px;border:0;outline:none;text-decoration:none;\"></td><td style=\"padding:0;vertical-align:middle;white-space:nowrap;\"><span style=\"color:#ffffff !important;-webkit-text-fill-color:#ffffff;font-size:22px;line-height:1;font-weight:700;letter-spacing:0;\">Legal</span><span style=\"color:#f26a2e !important;-webkit-text-fill-color:#f26a2e;font-size:22px;line-height:1;font-weight:700;letter-spacing:0;\">Synq</span></td></tr></table>");
    }

    private static void AppendEmailSectionHeading(StringBuilder body, string title)
    {
        body.Append("<tr><td bgcolor=\"#ffffff\" class=\"email-card\" style=\"padding:0 0 13px 0;background-color:#ffffff !important;\"><img src=\"cid:")
            .Append(EmailSectionIconContentId(title))
            .Append("\" width=\"22\" height=\"22\" alt=\"\" role=\"presentation\" style=\"display:inline-block;width:22px;height:22px;border:0;outline:none;text-decoration:none;vertical-align:middle;\"><span style=\"display:inline-block;margin-left:8px;color:#111111 !important;-webkit-text-fill-color:#111111;font-size:16px;font-weight:600;line-height:22px;letter-spacing:0;vertical-align:middle;\">")
            .Append(Html(title))
            .AppendLine("</span></td></tr>");
    }

    private static string EmailSectionIconContentId(string title)
        => title switch
        {
            "Seller Information" => ConfirmSaleEmailAssets.SellerInformationIconContentId,
            "Buyer Information" => ConfirmSaleEmailAssets.SellerInformationIconContentId,
            "Asset Overview" => ConfirmSaleEmailAssets.AssetOverviewIconContentId,
            "Supporting Documents" => ConfirmSaleEmailAssets.SupportingDocumentsIconContentId,
            _ => ConfirmSaleEmailAssets.AssetOverviewIconContentId,
        };

    private static void AppendDocumentsSection(
        StringBuilder body,
        IReadOnlyList<ConfirmSaleDocument> documents)
    {
        body.AppendLine("<table role=\"presentation\" width=\"100%\" cellspacing=\"0\" cellpadding=\"0\" bgcolor=\"#ffffff\" class=\"email-card\" style=\"width:100%;border-collapse:collapse;margin:0 0 24px 0;background-color:#ffffff !important;\">");
        AppendEmailSectionHeading(body, "Supporting Documents");
        body.AppendLine("<tr><td bgcolor=\"#ffffff\" class=\"email-card\" style=\"padding:0;background-color:#ffffff !important;\">");
        body.AppendLine("<table role=\"presentation\" width=\"100%\" cellspacing=\"0\" cellpadding=\"0\" bgcolor=\"#ffffff\" class=\"email-card email-rule\" style=\"width:100%;border-collapse:separate;border-spacing:0;background-color:#ffffff !important;\">");

        for (var i = 0; i < documents.Count; i++)
        {
            var document = documents[i];
            var isFirstRow = i == 0;
            var isLastRow = i == documents.Count - 1;
            var iconBorder = EmailTableCellBorder(isFirstRow, isLastRow, leftEdge: true, rightEdge: false);
            var labelBorder = EmailTableCellBorder(isFirstRow, isLastRow, leftEdge: false, rightEdge: false);
            var valueBorder = EmailTableCellBorder(isFirstRow, isLastRow, leftEdge: false, rightEdge: true);
            body.Append("<tr><td bgcolor=\"#ffffff\" class=\"email-card\" style=\"width:32px;padding:15px 0 15px 14px;background-color:#ffffff !important;")
                .Append(iconBorder)
                .Append("\"><span style=\"display:inline-block;width:18px;height:18px;line-height:18px;text-align:center;border-radius:5px;background-color:#f26a2e !important;color:#ffffff !important;font-size:12px;font-weight:700;\">&#10003;</span></td><td align=\"left\" bgcolor=\"#ffffff\" class=\"email-card email-label\" style=\"padding:15px 10px 15px 8px;color:#6f6f6f !important;background-color:#ffffff !important;font-size:14px;line-height:1.45;font-weight:500;")
                .Append(labelBorder)
                .Append("\">")
                .Append(Html(FirstNonEmpty(document.Category, "Document")!))
                .Append("</td><td align=\"right\" bgcolor=\"#ffffff\" class=\"email-card email-value\" style=\"padding:15px 14px 15px 10px;color:#111111 !important;background-color:#ffffff !important;font-size:15px;line-height:1.45;font-weight:500;")
                .Append(valueBorder)
                .Append("\">")
                .Append(Html(document.FileName))
                .AppendLine("</td></tr>");
        }

        body.AppendLine("</table>");
        body.AppendLine("</td></tr>");
        body.AppendLine("</table>");
    }

    private static string EmailTableCellBorder(bool isFirstRow, bool isLastRow, bool leftEdge, bool rightEdge)
    {
        var border = new StringBuilder();

        if (isFirstRow)
            border.Append("border-top:1px solid #e5e5e5;");

        border.Append("border-bottom:1px solid #e5e5e5;");

        if (leftEdge)
        {
            border.Append("border-left:1px solid #e5e5e5;");
            if (isFirstRow)
                border.Append("border-top-left-radius:10px;");
            if (isLastRow)
                border.Append("border-bottom-left-radius:10px;");
        }

        if (rightEdge)
        {
            border.Append("border-right:1px solid #e5e5e5;");
            if (isFirstRow)
                border.Append("border-top-right-radius:10px;");
            if (isLastRow)
                border.Append("border-bottom-right-radius:10px;");
        }

        return border.ToString();
    }

    private static string FormatStatusLabel(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
            return LienStatus.Offered;

        var trimmed = status.Trim();
        var label = new StringBuilder(trimmed.Length + 4);
        for (var i = 0; i < trimmed.Length; i++)
        {
            var current = trimmed[i];
            if (i > 0 &&
                char.IsUpper(current) &&
                char.IsLower(trimmed[i - 1]))
            {
                label.Append(' ');
            }

            label.Append(current);
        }

        return label.ToString();
    }

    private static string BuildConfirmSaleTextBody(
        string status,
        string intro,
        string publicPortalUrl,
        string ctaLabel,
        string footerCompany,
        string footerEmail,
        bool isSellerView,
        string informationSectionTitle,
        IReadOnlyList<(string Label, string? Value)> informationRows,
        IReadOnlyList<(string Label, string? Value)> assetRows,
        IReadOnlyList<ConfirmSaleDocument> documents)
    {
        var body = new StringBuilder();
        body.AppendLine("LegalSynq");
        body.AppendLine(status);
        body.AppendLine();
        body.AppendLine("New Lien Offer");
        body.AppendLine(intro);
        body.AppendLine();
        AppendTextSection(body, informationSectionTitle, informationRows);
        AppendTextSection(body, "Asset Overview", assetRows);

        if (documents.Count > 0)
        {
            body.AppendLine("Supporting Documents");
            foreach (var document in documents)
            {
                body.Append("- ")
                    .Append(FirstNonEmpty(document.Category, "Document"))
                    .Append(": ")
                    .AppendLine(document.FileName);
            }

            body.AppendLine();
        }

        body.Append(ctaLabel).Append(": ").AppendLine(publicPortalUrl);
        body.AppendLine("This Link Expires in 30 Days");
        body.Append(isSellerView ? "This offer was sent to " : "This offer was sent on behalf of the ")
            .Append(footerCompany)
            .Append(isSellerView ? ". Please contact " : ". Please reply directly to ")
            .Append(footerEmail)
            .AppendLine(" for any questions.");

        return body.ToString();
    }

    private static void AppendTextSection(
        StringBuilder body,
        string title,
        IReadOnlyList<(string Label, string? Value)> rows)
    {
        body.AppendLine(title);
        foreach (var (label, value) in rows)
        {
            if (string.IsNullOrWhiteSpace(value))
                continue;

            body.Append(label).Append(": ").AppendLine(value.Trim());
        }
        body.AppendLine();
    }

    private async Task<Contact?> ResolveHandlingLawFirmContactAsync(
        Guid tenantId,
        Case? caseEntity,
        CancellationToken ct)
    {
        if (caseEntity is null)
            return null;

        var metadata = ParseLegacyNoteFields(caseEntity.Notes);
        if (Guid.TryParse(metadata.GetValueOrDefault("lawFirmId"), out var lawFirmId))
        {
            var lawFirm = await _contactRepo.GetByIdAsync(tenantId, lawFirmId, ct);
            if (IsActiveStandaloneLawFirm(lawFirm))
                return lawFirm;
        }

        var contacts = await _contactRepo.GetByOrgIdAsync(tenantId, caseEntity.OrgId, isActive: true, ct);
        return contacts.FirstOrDefault(c =>
            string.Equals(c.ContactType, ContactType.LawFirm, StringComparison.Ordinal) &&
            string.IsNullOrWhiteSpace(c.ContactSubtype) &&
            !c.LawFirmId.HasValue);
    }

    private static bool IsActiveStandaloneLawFirm(Contact? contact)
        => contact is { IsActive: true } &&
           string.Equals(contact.ContactType, ContactType.LawFirm, StringComparison.Ordinal) &&
           string.IsNullOrWhiteSpace(contact.ContactSubtype) &&
           !contact.LawFirmId.HasValue;

    private static string? ResolveHandlingLawFirmName(Contact? contact)
        => FirstNonEmpty(contact?.Organization, contact?.DisplayName);

    private async Task<string?> ResolveCaseManagerAsync(
        Guid tenantId,
        Case? caseEntity,
        CancellationToken ct)
    {
        if (caseEntity is null)
            return null;

        var metadata = ParseLegacyNoteFields(caseEntity.Notes);
        if (!Guid.TryParse(metadata.GetValueOrDefault("caseManagerId"), out var caseManagerId))
            return null;

        var caseManager = await _contactRepo.GetByIdAsync(tenantId, caseManagerId, ct);
        return FirstNonEmpty(caseManager?.DisplayName);
    }

    private async Task<List<ConfirmSaleDocument>> GetSupportingDocumentsAsync(
        Guid tenantId,
        Lien lien,
        CancellationToken ct)
    {
        var lienDocs = await _servicingItemRepo.SearchAsync(
            tenantId,
            search: null,
            status: null,
            priority: null,
            assignedTo: null,
            caseId: null,
            lienId: lien.Id,
            page: 1,
            pageSize: 100,
            ct: ct);

        return ExtractDocuments(lienDocs.Items)
            .Where(document => !string.IsNullOrWhiteSpace(document.FileName))
            .DistinctBy(document => document.FileName, StringComparer.OrdinalIgnoreCase)
            .OrderBy(document => document.FileName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static IEnumerable<ConfirmSaleDocument> ExtractDocuments(IEnumerable<ServicingItem> items)
    {
        foreach (var item in items.Where(IsDocumentServicingItem))
        {
            var fields = ParseLegacyNoteFields(item.Notes);
            var fileName = FirstNonEmpty(
                fields.GetValueOrDefault("originalFileName"),
                fields.GetValueOrDefault("displayName"),
                fields.GetValueOrDefault("filename"),
                item.Description);

            if (string.IsNullOrWhiteSpace(fileName))
                continue;

            var category = FirstNonEmpty(
                fields.GetValueOrDefault("documentCategory"),
                fields.GetValueOrDefault("category"),
                FormatSellingDocumentType(fields.GetValueOrDefault("documentType")),
                HumanizeDocumentTaskType(item.TaskType));

            yield return new ConfirmSaleDocument(fileName.Trim(), category);
        }
    }

    private static bool IsDocumentServicingItem(ServicingItem item)
        => string.Equals(item.TaskType, "LegacyCaseDocument", StringComparison.Ordinal) ||
           string.Equals(item.TaskType, "LegacyLienDocument", StringComparison.Ordinal) ||
           string.Equals(item.TaskType, "LegacyMedicalDocument", StringComparison.Ordinal) ||
           string.Equals(item.TaskType, "SellingDocumentReference", StringComparison.Ordinal);

    private static string HumanizeDocumentTaskType(string taskType)
        => taskType switch
        {
            "LegacyCaseDocument" => "Case Document",
            "LegacyLienDocument" => "Lien Document",
            "LegacyMedicalDocument" => "Medical Document",
            "SellingDocumentReference" => "Supporting Document",
            _ => "Document",
        };

    private static string? FormatSellingDocumentType(string? documentType)
    {
        if (string.IsNullOrWhiteSpace(documentType))
            return null;

        return documentType.Trim() switch
        {
            "LienAgreement" => "Signed Lien / LOP (Letter of Protection)",
            "MedicalBill" => "Itemized Bill / HCFA-1500 Form",
            "MedicalRecord" => "Clinical Chart Notes / Medical Records",
            "SettlementStatement" => "Settlement Statement",
            "Other" => "Supporting Document",
            var value => SplitCamelCase(value),
        };
    }

    private static string SplitCamelCase(string value)
    {
        var label = new StringBuilder(value.Length + 8);
        for (var i = 0; i < value.Length; i++)
        {
            var current = value[i];
            if (i > 0 && char.IsUpper(current) && char.IsLower(value[i - 1]))
                label.Append(' ');

            label.Append(current);
        }

        return label.ToString();
    }

    private static Contact? SelectSellerContact(IReadOnlyList<Contact> contacts, Guid? excludedContactId = null)
    {
        var orderedContacts = OrderSellerContacts(contacts);
        var preferredContacts = excludedContactId.HasValue
            ? orderedContacts.Where(contact => contact.Id != excludedContactId.Value).ToList()
            : orderedContacts;

        return SelectSellerContactWithEmail(preferredContacts)
           ?? (excludedContactId.HasValue ? SelectSellerContactWithEmail(orderedContacts) : null)
           ?? preferredContacts.FirstOrDefault()
           ?? orderedContacts.FirstOrDefault();
    }

    private static Contact? SelectSellerContactWithEmail(IReadOnlyList<Contact> contacts)
        => contacts.FirstOrDefault(c =>
               string.Equals(c.ContactType, ContactType.LawFirm, StringComparison.Ordinal) &&
               string.IsNullOrWhiteSpace(c.ContactSubtype) &&
               !string.IsNullOrWhiteSpace(c.Email))
           ?? contacts.FirstOrDefault(c => !string.IsNullOrWhiteSpace(c.Email));

    private static IReadOnlyList<Contact> OrderSellerContacts(IReadOnlyList<Contact> contacts)
        => contacts
            .OrderBy(c => c.DisplayName)
            .ThenBy(c => c.Email ?? string.Empty)
            .ThenBy(c => c.Id)
            .ToList();

    private static ConfirmSellingLienSaleResponse MapConfirmSaleResponse(
        Lien lien,
        ConfirmSellingLienBuyerNotificationResponse? notification,
        ConfirmSellingLienSellerNotificationResponse? sellerNotification)
        => new()
        {
            LienId = lien.Id,
            LienCode = ResolveLienCode(lien),
            Status = lien.Status,
            SellerStatus = lien.SellerStatus ?? string.Empty,
            AskAmount = lien.AskAmount,
            OfferPrice = lien.OfferPrice,
            SubmittedForSaleAtUtc = lien.SubmittedForSaleAtUtc,
            SoldAtUtc = lien.SoldAtUtc,
            Notification = notification,
            SellerNotification = sellerNotification,
        };

    private static string BuildConfirmSaleNotificationIdempotencyKey(
        Guid tenantId,
        Guid lienId,
        Guid buyerContactId)
    {
        var key = string.Join(":", new[]
        {
            "liens.confirm-sale.email",
            tenantId.ToString("N"),
            lienId.ToString("N"),
            buyerContactId.ToString("N"),
        });

        return key.Length > 280 ? key[..280] : key;
    }

    private static string BuildConfirmSaleSellerNotificationIdempotencyKey(
        Guid tenantId,
        Guid lienId,
        Guid sellerContactId,
        Guid buyerContactId)
    {
        var key = string.Join(":", new[]
        {
            "liens.confirm-sale.seller-email",
            tenantId.ToString("N"),
            lienId.ToString("N"),
            sellerContactId.ToString("N"),
            buyerContactId.ToString("N"),
        });

        return key.Length > 280 ? key[..280] : key;
    }

    private static bool IsSubmittedNotificationStatus(string? status)
        => string.Equals(status, "sent", StringComparison.OrdinalIgnoreCase) ||
           string.Equals(status, "accepted", StringComparison.OrdinalIgnoreCase) ||
           string.Equals(status, "queued", StringComparison.OrdinalIgnoreCase);

    private static Dictionary<string, string> ParseLegacyNoteFields(string? notes)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(notes))
            return result;

        var trimmed = notes.Trim();
        if (trimmed.StartsWith('{'))
        {
            try
            {
                using var document = JsonDocument.Parse(trimmed);
                if (document.RootElement.ValueKind == JsonValueKind.Object)
                {
                    foreach (var property in document.RootElement.EnumerateObject())
                    {
                        var value = property.Value.ValueKind == JsonValueKind.String
                            ? property.Value.GetString()
                            : property.Value.GetRawText();
                        if (!string.IsNullOrWhiteSpace(value))
                            result[property.Name] = value;
                    }
                }
            }
            catch (JsonException)
            {
                // Fall back to the legacy key/value parser below.
            }
        }

        foreach (var segment in notes.Split("; ", StringSplitOptions.RemoveEmptyEntries))
        {
            var eq = segment.IndexOf('=');
            if (eq <= 0)
                continue;

            var key = segment[..eq].Trim();
            var value = segment[(eq + 1)..].Trim();
            if (!string.IsNullOrWhiteSpace(key))
                result[key] = value;
        }

        return result;
    }

    private static string ResolveLienCode(Lien lien)
        => string.IsNullOrWhiteSpace(lien.LienNumber) ? lien.Id.ToString() : lien.LienNumber;

    private static string? FirstNonEmpty(params string?[] values)
        => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim();

    private static string ResolveContactPersonName(Contact contact)
        => string.Join(' ', new[] { contact.FirstName, contact.LastName }
            .Where(part => !string.IsNullOrWhiteSpace(part)))
            .Trim();

    private static string Html(string value)
        => WebUtility.HtmlEncode(value);

    private sealed record ConfirmSaleNotificationContext(
        Contact BuyerContact,
        Contact SellerContact,
        SellerOrganizationDisplay SellerDisplay,
        string SellerEmail,
        Case? Case,
        Contact HandlingLawFirmContact,
        string? CaseManager,
        IReadOnlyList<ConfirmSaleDocument> Documents);

    private sealed record ConfirmSaleDocument(string FileName, string? Category);

    private sealed record ConfirmSaleEmail(
        string Subject,
        string HtmlBody,
        string TextBody,
        Dictionary<string, string> TemplateData,
        IReadOnlyList<NotificationEmailInlineAttachment> InlineAttachments);

    private enum ConfirmSaleEmailAudience
    {
        Buyer,
        Seller,
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

    private async Task<Lien?> ResolveLienAsync(Guid tenantId, string lienIdOrCode, CancellationToken ct)
    {
        var value = lienIdOrCode.Trim();
        return Guid.TryParse(value, out var lienId)
            ? await _lienRepo.GetByIdAsync(tenantId, lienId, ct)
            : await _lienRepo.GetByLienNumberAsync(tenantId, value, ct);
    }

    private async Task<Lien?> ResolveLienForAssignmentAsync(Guid tenantId, string lienIdOrCode, CancellationToken ct)
    {
        var value = lienIdOrCode.Trim();
        return Guid.TryParse(value, out var lienId)
            ? await _lienRepo.GetByIdAnyTenantAsync(lienId, ct)
            : await _lienRepo.GetByLienNumberAsync(tenantId, value, ct);
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

    private static List<string> BuildLienAssignmentRequests(AddSellingPortfolioLiensRequest request)
    {
        var result = new List<string>();
        result.AddRange(request.LienIds.Select(id => id.ToString()));
        result.AddRange(request.LienCodes);
        result.AddRange(request.Liens);
        return result;
    }

    private static Guid TryParseLienId(string lienIdOrCode) =>
        Guid.TryParse(lienIdOrCode, out var lienId) ? lienId : Guid.Empty;

    private static AddSellingPortfolioLienResult FailedLienResult(
        string requestedLien,
        Guid lienId,
        string? lienCode,
        string reasonCode,
        string message) => new()
        {
            RequestedLien = requestedLien,
            LienId = lienId,
            LienCode = lienCode,
            Success = false,
            Status = "rejected",
            ReasonCode = reasonCode,
            Message = message,
        };

    private void LogEligibilityFailure(
        Guid tenantId,
        Guid actingUserId,
        SellingPortfolio portfolio,
        Lien lien,
        LienEligibilityValidationResult eligibility)
    {
        var ruleCodes = string.Join(",", eligibility.Violations.Select(v => v.RuleCode));
        var messages = string.Join(" ", eligibility.Violations.Select(v => v.Message));

        _audit.Publish(
            eventType: "liens.selling_portfolio.lien_eligibility_failed",
            action: "LIEN_PORTFOLIO_ELIGIBILITY_VALIDATION_FAILED",
            description: $"Lien '{lien.LienNumber}' failed portfolio eligibility validation: {messages}",
            tenantId: tenantId,
            actorUserId: actingUserId,
            entityType: "Lien",
            entityId: lien.Id.ToString(),
            metadata: $"{{\"portfolioId\":\"{portfolio.Id}\",\"lienId\":\"{lien.Id}\",\"ruleCodes\":\"{ruleCodes}\"}}");
    }

    private static RemoveSellingPortfolioLienResult FailedRemoveLienResult(
        Guid lienId,
        string? lienCode,
        string reasonCode,
        string message) => new()
        {
            LienId = lienId,
            LienCode = lienCode,
            Success = false,
            Status = "rejected",
            ReasonCode = reasonCode,
            Message = message,
        };

    private static string BuildPlaintiffName(Case? caseEntity, Lien lien)
    {
        var firstName = caseEntity?.ClientFirstName ?? lien.SubjectFirstName;
        var lastName = caseEntity?.ClientLastName ?? lien.SubjectLastName;
        var name = string.Join(" ", new[] { firstName, lastName }
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!.Trim()));

        return string.IsNullOrWhiteSpace(name) ? "Unknown Plaintiff" : name;
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
            InternalNotes = entity.InternalNotes,
            TargetGrouping = entity.TargetGrouping,
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

    private async Task InTransactionAsync(Func<Task> operation, CancellationToken ct)
    {
        await using var transaction = await _unitOfWork.BeginTransactionAsync(ct);
        try
        {
            await operation();
            await transaction.CommitAsync(ct);
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            throw;
        }
    }

    private async Task AddActivityAsync(
        SellingPortfolio portfolio,
        Guid actorUserId,
        string action,
        string entityType,
        string summary,
        string? entityId = null,
        string? metadataJson = null,
        CancellationToken ct = default)
    {
        var activity = SellingPortfolioActivity.Create(
            portfolio.TenantId,
            portfolio.Id,
            action,
            entityType,
            actorUserId,
            summary,
            entityId,
            metadataJson);

        await _portfolioRepo.AddActivityAsync(activity, ct);
    }

    private static string ResolveStatusActivityAction(string status) => status switch
    {
        SellingPortfolioStatus.Published => "LIEN_SALE_PORTFOLIO_PUBLISHED",
        SellingPortfolioStatus.Withdrawn => "LIEN_SALE_PORTFOLIO_WITHDRAWN",
        _ => "LIEN_SALE_PORTFOLIO_STATUS_CHANGED",
    };

    private static List<SellingPortfolioAgingBucket> BuildAgingBuckets(IEnumerable<SellingPortfolioLien> liens)
    {
        var buckets = new Dictionary<string, (int Count, decimal Balance)>
        {
            ["0-30"] = (0, 0m),
            ["31-60"] = (0, 0m),
            ["61-90"] = (0, 0m),
            ["91-120"] = (0, 0m),
            ["120+"] = (0, 0m),
        };

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        foreach (var lien in liens)
        {
            var ageDays = lien.IncidentDate.HasValue
                ? Math.Max(0, today.DayNumber - lien.IncidentDate.Value.DayNumber)
                : Math.Max(0, (DateTime.UtcNow.Date - lien.CreatedAtUtc.Date).Days);
            var bucket = ageDays switch
            {
                <= 30 => "0-30",
                <= 60 => "31-60",
                <= 90 => "61-90",
                <= 120 => "91-120",
                _ => "120+",
            };
            var current = buckets[bucket];
            buckets[bucket] = (current.Count + 1, current.Balance + (lien.CurrentBalance ?? 0m));
        }

        return buckets.Select(kvp => new SellingPortfolioAgingBucket
        {
            Bucket = kvp.Key,
            LienCount = kvp.Value.Count,
            OutstandingBalance = kvp.Value.Balance,
        }).ToList();
    }

    private static List<SellingPortfolioConcentrationItem> BuildConcentrations(IEnumerable<SellingPortfolioLien> liens)
    {
        return liens
            .GroupBy(l => string.IsNullOrWhiteSpace(l.Jurisdiction) ? "Unknown" : l.Jurisdiction)
            .Select(g => new SellingPortfolioConcentrationItem
            {
                Dimension = "Jurisdiction",
                Value = g.Key!,
                LienCount = g.Count(),
                OutstandingBalance = g.Sum(l => l.CurrentBalance ?? 0m),
            })
            .OrderByDescending(item => item.OutstandingBalance)
            .Take(10)
            .ToList();
    }

    private static SellingPortfolioActivityResponse MapActivity(SellingPortfolioActivity entity) => new()
    {
        Id = entity.Id,
        PortfolioId = entity.PortfolioId,
        Action = entity.Action,
        EntityType = entity.EntityType,
        EntityId = entity.EntityId,
        ActorUserId = entity.ActorUserId,
        OccurredAtUtc = entity.OccurredAtUtc,
        Summary = entity.Summary,
        MetadataJson = entity.MetadataJson,
    };
}
