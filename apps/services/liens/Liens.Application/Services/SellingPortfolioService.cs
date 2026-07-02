using System.Globalization;
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
    private readonly IContactRepository _contactRepo;
    private readonly ILienSettlementRepository _settlementRepo;
    private readonly ISettlementPaymentDetailRepository _paymentDetailRepo;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuditPublisher _audit;
    private readonly INotificationPublisher _notifications;
    private readonly ILienEligibilityValidator _eligibilityValidator;
    private readonly ILogger<SellingPortfolioService> _logger;

    public SellingPortfolioService(
        ISellingPortfolioRepository portfolioRepo,
        ILienRepository lienRepo,
        ICaseRepository caseRepo,
        IContactRepository contactRepo,
        ILienSettlementRepository settlementRepo,
        ISettlementPaymentDetailRepository paymentDetailRepo,
        IUnitOfWork unitOfWork,
        IAuditPublisher audit,
        INotificationPublisher notifications,
        ILienEligibilityValidator eligibilityValidator,
        ILogger<SellingPortfolioService> logger)
    {
        _portfolioRepo = portfolioRepo;
        _lienRepo = lienRepo;
        _caseRepo = caseRepo;
        _contactRepo = contactRepo;
        _settlementRepo = settlementRepo;
        _paymentDetailRepo = paymentDetailRepo;
        _unitOfWork = unitOfWork;
        _audit = audit;
        _notifications = notifications;
        _eligibilityValidator = eligibilityValidator;
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
