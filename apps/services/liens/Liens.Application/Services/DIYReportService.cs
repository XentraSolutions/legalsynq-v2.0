using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Liens.Application.DTOs;
using Liens.Application.Interfaces;
using Liens.Application.Repositories;
using Liens.Domain.Entities;
using Liens.Domain.Enums;

namespace Liens.Application.Services;

public class DIYReportService : IDIYReportService
{
    private readonly IDIYReportConfigRepository _repo;
    private readonly ILienRepository            _lienRepo;
    private readonly ICaseRepository            _caseRepo;
    private readonly IServicingItemRepository   _servicingItemRepo;
    private readonly IContactRepository         _contactRepo;
    private readonly IFacilityRepository        _facilityRepo;
    private readonly ILookupValueRepository     _lookupRepo;
    private readonly ILienReductionRepository   _reductionRepo;
    private readonly ILienSettlementRepository  _settlementRepo;
    private readonly ISettlementPaymentDetailRepository _paymentDetailRepo;
    private readonly ILienCaseNoteRepository _caseNoteRepo;

    public DIYReportService(
        IDIYReportConfigRepository repo,
        ILienRepository lienRepo,
        ICaseRepository caseRepo,
        IServicingItemRepository servicingItemRepo,
        IContactRepository contactRepo,
        IFacilityRepository facilityRepo,
        ILookupValueRepository lookupRepo,
        ILienReductionRepository reductionRepo,
        ILienSettlementRepository settlementRepo,
        ISettlementPaymentDetailRepository paymentDetailRepo,
        ILienCaseNoteRepository caseNoteRepo)
    {
        _repo              = repo;
        _lienRepo          = lienRepo;
        _caseRepo          = caseRepo;
        _servicingItemRepo = servicingItemRepo;
        _contactRepo       = contactRepo;
        _facilityRepo      = facilityRepo;
        _lookupRepo        = lookupRepo;
        _reductionRepo     = reductionRepo;
        _settlementRepo    = settlementRepo;
        _paymentDetailRepo = paymentDetailRepo;
        _caseNoteRepo      = caseNoteRepo;
    }

    public async Task<List<DIYReportConfigResponse>> GetSavedReportsAsync(
        Guid tenantId, Guid userId, CancellationToken ct = default)
    {
        var items = await _repo.GetByUserAsync(tenantId, userId, ct);
        return items.Select(Map).ToList();
    }

    public async Task<DIYReportConfigResponse> GetByIdAsync(
        Guid tenantId, Guid id, CancellationToken ct = default)
    {
        var entity = await _repo.GetByIdAsync(tenantId, id, ct)
            ?? throw new KeyNotFoundException($"Report config {id} not found.");
        return Map(entity);
    }

    public async Task<DIYReportConfigResponse> SaveReportAsync(
        Guid tenantId, Guid userId, SaveDIYReportRequest request, CancellationToken ct = default)
    {
        var configJson = BuildPersistedConfigJson(request);
        var entity = DIYReportConfig.Create(tenantId, userId, request.Name, configJson, userId);
        await _repo.AddAsync(entity, ct);
        return Map(entity);
    }

    public async Task DeleteReportAsync(
        Guid tenantId, Guid id, Guid userId, CancellationToken ct = default)
    {
        var entity = await _repo.GetByIdAsync(tenantId, id, ct)
            ?? throw new KeyNotFoundException($"Report config {id} not found.");
        entity.SoftDelete(userId);
        await _repo.UpdateAsync(entity, ct);
    }

    /// <summary>
    /// Executes the DIY report by reading the "status" filter from the config
    /// JSON and delegating to the case search.  Additional column/filter support
    /// can be added here as the product evolves.
    /// </summary>
    public async Task<DIYReportResult> RunReportAsync(
        Guid tenantId,
        DIYReportRunRequest request,
        bool includeAllItems = false,
        CancellationToken ct = default)
    {
        // Extract known filters from the config payload
        string? search = null;
        var requestedLienStatuses = new List<string>();
        var statusViewLienStatuses = new List<string>();
        var caseStatuses = new List<string>();
        var reportType = NormalizeReportType(GetReportString(request, "reportType"));
        var isLiensReport = string.Equals(reportType, "LIENS", StringComparison.OrdinalIgnoreCase);
        var isCombinedReport = string.Equals(reportType, "COMBINED", StringComparison.OrdinalIgnoreCase);
        var isCasesReport = !isLiensReport && !isCombinedReport;

        if (HasReportProperty(request, "status", out var s) && s.ValueKind == JsonValueKind.String)
        {
            var status = s.GetString();
            if (!string.IsNullOrWhiteSpace(status))
                requestedLienStatuses.Add(status.Trim());
        }
        if (HasReportProperty(request, "statusView", out var statusView) && statusView.ValueKind == JsonValueKind.String)
        {
            var status = statusView.GetString();
            if (!string.IsNullOrWhiteSpace(status))
            {
                ApplyStatusView(
                    status.Trim(),
                    isLiensReport || isCombinedReport,
                    statusViewLienStatuses,
                    caseStatuses);
            }
        }
        if (HasReportProperty(request, "lienStatusIds", out var statusIds) && statusIds.ValueKind == JsonValueKind.Array)
        {
            var requestedStatuses = statusIds.EnumerateArray()
                .Where(item => item.ValueKind is JsonValueKind.String or JsonValueKind.Number)
                .Select(item => item.ValueKind == JsonValueKind.String ? item.GetString() : item.GetRawText())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value!.Trim())
                .ToList();
            requestedLienStatuses.AddRange(await ResolveLienStatusesAsync(tenantId, requestedStatuses, ct));
        }
        var lienStatuses = CombineLienStatusFilters(requestedLienStatuses, statusViewLienStatuses);
        if (HasReportProperty(request, "search", out var k) && k.ValueKind == JsonValueKind.String)
            search = k.GetString();
        var purchaseDateFrom = GetReportDateOnly(request, "purchaseDateFrom");
        var purchaseDateTo = GetReportDateOnly(request, "purchaseDateTo");
        var closedDateFrom = GetReportDateTime(request, "closedDateFrom", endOfDay: false);
        var closedDateTo = GetReportDateTime(request, "closedDateTo", endOfDay: true);
        var isBulk = GetReportString(request, "isBulk");
        var normalizedBulkFilter = NormalizeDIYBulkFilter(isBulk);
        var filterCaseIds = GetReportGuidList(request, "plaintiffCaseIds");
        var relationshipFilters = new ReportRelationshipFilters(
            GetReportGuidList(request, "lawFirmIds").ToHashSet(),
            GetReportGuidList(request, "attorneyIds").ToHashSet(),
            GetReportGuidList(request, "fundingCompanyIds").ToHashSet(),
            GetReportGuidList(request, "medicalFacilityIds").ToHashSet(),
            GetReportGuidList(request, "caseManagerIds").ToHashSet(),
            GetReportGuidList(request, "medicalProviderIds").ToHashSet());

        var page = GetReportInt(request, "page", request.Page);
        if (page < 1) page = 1;
        var limit = GetReportInt(request, "limit", request.Limit);
        if (limit < 1) limit = 50;

        var reportData = await _lienRepo.SearchReportAsync(
            tenantId,
            search,
            lienStatuses.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            caseStatuses.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            purchaseDateFrom,
            purchaseDateTo,
            closedDateFrom,
            closedDateTo,
            isCasesReport,
            normalizedBulkFilter,
            filterCaseIds,
            page,
            limit,
            ct);

        var caseIds = reportData.AllItems
            .Select(l => l.CaseId)
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Distinct()
            .ToList();
        var casesById = (await _caseRepo.GetByIdsAsync(tenantId, caseIds, ct))
            .ToDictionary(caseEntity => caseEntity.Id);
        var servicingItems = await _servicingItemRepo.GetByLienIdsAsync(
            tenantId,
            reportData.AllItems.Select(lien => lien.Id).Distinct().ToList(),
            ct);
        var filteredLiens = ApplyRelationshipFilters(
            reportData.AllItems,
            casesById,
            servicingItems,
            relationshipFilters);
        var totalCount = isCasesReport
            ? filteredLiens.Select(lien => lien.CaseId).Where(id => id.HasValue).Distinct().Count()
            : filteredLiens.Count;
        var effectivePage = includeAllItems ? 1 : page;
        var effectiveLimit = includeAllItems ? int.MaxValue : limit;
        var pageLiens = includeAllItems
            ? filteredLiens
            : TakePage(filteredLiens, effectivePage, effectiveLimit);
        var filteredCaseIds = filteredLiens
            .Select(lien => lien.CaseId)
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Distinct()
            .ToHashSet();
        var reportCasesById = casesById
            .Where(pair => filteredCaseIds.Contains(pair.Key))
            .ToDictionary(pair => pair.Key, pair => pair.Value);

        if (CanIncludeUnlinkedCases(
                isCasesReport,
                lienStatuses,
                purchaseDateFrom,
                purchaseDateTo,
                closedDateFrom,
                closedDateTo,
                normalizedBulkFilter,
                relationshipFilters))
        {
            var unlinkedCases = await _caseRepo.SearchUnlinkedReportCasesAsync(
                tenantId,
                search,
                caseStatuses,
                filterCaseIds,
                relationshipFilters.LawFirmIds,
                relationshipFilters.AttorneyIds,
                relationshipFilters.CaseManagerIds,
                ct);
            foreach (var caseEntity in unlinkedCases)
                reportCasesById[caseEntity.Id] = caseEntity;
        }

        totalCount = isCasesReport ? reportCasesById.Count : filteredLiens.Count;
        var rowCasesById = isCasesReport
            ? reportCasesById
            : pageLiens
                .Where(lien => lien.CaseId.HasValue && casesById.ContainsKey(lien.CaseId.Value))
                .Select(lien => lien.CaseId!.Value)
                .Distinct()
                .ToDictionary(caseId => caseId, caseId => casesById[caseId]);

        var legacyMedicalAmounts = GetLegacyMedicalAmountsByLienId(filteredLiens, servicingItems);
        var rowEnrichment = await GetRowEnrichmentAsync(
            tenantId,
            filteredLiens,
            rowCasesById,
            servicingItems,
            ShouldIncludeTrackingNotes(request),
            ShouldIncludeCaseActivity(request),
            ShouldIncludeFeedNotes(request),
            ct);

        var rows = isCasesReport
            ? BuildCaseRows(filteredLiens, rowCasesById, legacyMedicalAmounts, rowEnrichment, effectivePage, effectiveLimit)
            : pageLiens.Select(lien => BuildLienRow(lien, rowCasesById, legacyMedicalAmounts, rowEnrichment)).ToList();

        var summary = BuildSummary(
            filteredLiens,
            legacyMedicalAmounts,
            rowEnrichment,
            isCasesReport ? reportCasesById : casesById,
            isCombinedReport,
            ct);

        return new DIYReportResult
        {
            ReportType = reportType,
            Items      = rows,
            TotalCount = totalCount,
            Page       = effectivePage,
            PageSize   = includeAllItems ? totalCount : limit,
            SummaryTotals = summary,
        };
    }

    private async Task<IReadOnlyCollection<string>> ResolveLienStatusesAsync(
        Guid tenantId,
        IReadOnlyCollection<string> requestedStatuses,
        CancellationToken ct)
    {
        var lookupIds = requestedStatuses
            .Where(value => Guid.TryParse(value, out _))
            .Select(Guid.Parse)
            .ToHashSet();
        if (lookupIds.Count == 0)
            return requestedStatuses;

        var lookups = await _lookupRepo.GetByCategoryAsync(tenantId, LookupCategory.LienStatus, ct);
        var lookupCodesById = lookups
            .Where(lookup => lookupIds.Contains(lookup.Id))
            .ToDictionary(lookup => lookup.Id, lookup => lookup.Code);

        return requestedStatuses
            .Select(value => Guid.TryParse(value, out var lookupId) &&
                             lookupCodesById.TryGetValue(lookupId, out var code)
                ? code
                : value)
            .ToList();
    }

    private static List<Lien> ApplyRelationshipFilters(
        IReadOnlyCollection<Lien> liens,
        IReadOnlyDictionary<Guid, Case> casesById,
        IReadOnlyCollection<ServicingItem> servicingItems,
        ReportRelationshipFilters filters)
    {
        if (!filters.HasAny)
            return liens.ToList();

        var caseMetadataById = casesById.Values.ToDictionary(
            caseEntity => caseEntity.Id,
            caseEntity => ParseLegacyNoteFields(caseEntity.Notes));
        var facilityMetadataByLienId = servicingItems
            .Where(item => item.LienId.HasValue &&
                           string.Equals(item.TaskType, "LegacyMedicalFacilityInfo", StringComparison.Ordinal))
            .GroupBy(item => item.LienId!.Value)
            .ToDictionary(
                group => group.Key,
                group => ParseLegacyNoteFields(group.OrderBy(item => item.CreatedAtUtc).First().Notes));

        return liens.Where(lien =>
        {
            var caseMetadata = lien.CaseId.HasValue &&
                               caseMetadataById.TryGetValue(lien.CaseId.Value, out var metadata)
                ? metadata
                : EmptyLegacyMetadata;
            var facilityMetadata = facilityMetadataByLienId.TryGetValue(lien.Id, out var lienMetadata)
                ? lienMetadata
                : EmptyLegacyMetadata;

            return MatchesMetadataIdFilter(filters.LawFirmIds, caseMetadata, "lawFirmId") &&
                   MatchesMetadataIdFilter(filters.AttorneyIds, caseMetadata, "attorneyId") &&
                   MatchesGuidFilter(filters.FundingCompanyIds, lien.FundingCompanyId) &&
                   MatchesAnyGuidFilter(
                       filters.MedicalFacilityIds,
                       lien.FacilityId,
                       FirstGuid(facilityMetadata.GetValueOrDefault("facilityId"))) &&
                   MatchesMetadataIdFilter(filters.CaseManagerIds, caseMetadata, "caseManagerId") &&
                   MatchesAnyGuidFilter(
                       filters.MedicalProviderIds,
                       FirstGuid(facilityMetadata.GetValueOrDefault("medicalProviderId")));
        }).ToList();
    }

    private static bool MatchesMetadataIdFilter(
        IReadOnlySet<Guid> requestedIds,
        IReadOnlyDictionary<string, string> metadata,
        string key) =>
        requestedIds.Count == 0 ||
        (Guid.TryParse(metadata.GetValueOrDefault(key), out var id) && requestedIds.Contains(id));

    private static bool MatchesGuidFilter(IReadOnlySet<Guid> requestedIds, Guid? candidate) =>
        requestedIds.Count == 0 || candidate.HasValue && requestedIds.Contains(candidate.Value);

    private static bool MatchesAnyGuidFilter(IReadOnlySet<Guid> requestedIds, params Guid?[] candidates) =>
        requestedIds.Count == 0 || candidates.Any(candidate => candidate.HasValue && requestedIds.Contains(candidate.Value));

    private static List<Lien> TakePage(IReadOnlyList<Lien> liens, int page, int limit)
    {
        var skip = (long)(page - 1) * limit;
        return skip >= liens.Count
            ? []
            : liens.Skip((int)skip).Take(limit).ToList();
    }

    private static string NormalizeReportType(string? reportType) =>
        reportType?.Trim().ToUpperInvariant() switch
        {
            "CASE" or "CASES" => "CASES",
            "COMBINED" => "COMBINED",
            _ => "LIENS",
        };

    private static void ApplyStatusView(
        string statusView,
        bool isLienLevel,
        List<string> lienStatuses,
        List<string> caseStatuses)
    {
        switch (statusView.ToUpperInvariant())
        {
            case "ALL":
                return;
            case "OPEN":
                if (isLienLevel)
                    lienStatuses.AddRange(LienStatus.Open);
                else
                    caseStatuses.AddRange(CaseStatus.All.Where(s => !IsClosedCaseStatus(s)));
                return;
            case "CLOSED":
                if (isLienLevel)
                    lienStatuses.Add(LienStatus.Settled);
                else
                    caseStatuses.AddRange(CaseStatus.All.Where(IsClosedCaseStatus));
                return;
            case "REJECTED":
                if (isLienLevel)
                    lienStatuses.AddRange([LienStatus.Declined, LienStatus.Withdrawn, LienStatus.Cancelled]);
                return;
            default:
                if (isLienLevel && LienStatus.All.Contains(statusView))
                    lienStatuses.Add(statusView);
                else if (CaseStatus.All.Contains(statusView))
                    caseStatuses.Add(statusView);
                return;
        }
    }

    private static IReadOnlyCollection<string> CombineLienStatusFilters(
        IReadOnlyCollection<string> requestedStatuses,
        IReadOnlyCollection<string> statusViewStatuses)
    {
        var expandedRequested = LienStatus.ExpandFilterValues(requestedStatuses);
        var expandedStatusView = LienStatus.ExpandFilterValues(statusViewStatuses);

        if (expandedRequested.Count == 0)
            return expandedStatusView.ToList();
        if (expandedStatusView.Count == 0)
            return expandedRequested.ToList();

        var intersection = expandedRequested
            .Intersect(expandedStatusView, StringComparer.OrdinalIgnoreCase)
            .ToList();

        // An empty list means "no status filter" to the repository, so retain an
        // impossible sentinel when both valid filter dimensions have no overlap.
        return intersection.Count > 0 ? intersection : ["__NO_MATCHING_LIEN_STATUS__"];
    }

    private static readonly IReadOnlyDictionary<string, string> EmptyLegacyMetadata =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    private sealed record ReportRelationshipFilters(
        IReadOnlySet<Guid> LawFirmIds,
        IReadOnlySet<Guid> AttorneyIds,
        IReadOnlySet<Guid> FundingCompanyIds,
        IReadOnlySet<Guid> MedicalFacilityIds,
        IReadOnlySet<Guid> CaseManagerIds,
        IReadOnlySet<Guid> MedicalProviderIds)
    {
        public bool HasAny =>
            LawFirmIds.Count > 0 ||
            AttorneyIds.Count > 0 ||
            FundingCompanyIds.Count > 0 ||
            MedicalFacilityIds.Count > 0 ||
            CaseManagerIds.Count > 0 ||
            MedicalProviderIds.Count > 0;
    }

    private static DIYReportRow BuildLienRow(
        Lien l,
        IReadOnlyDictionary<Guid, Case> casesById,
        IReadOnlyDictionary<Guid, LegacyMedicalAmounts> legacyMedicalAmounts,
        ReportRowEnrichment enrichment)
    {
        Case? caseEntity = null;
        if (l.CaseId.HasValue)
            casesById.TryGetValue(l.CaseId.Value, out caseEntity);
        var caseDetails = caseEntity is not null && enrichment.CaseDetailsById.TryGetValue(caseEntity.Id, out var details)
            ? details
            : CaseReportDetails.Empty;
        var billingAmount = ResolveBillingAmount(l, legacyMedicalAmounts);
        var purchaseAmount = ResolvePurchaseAmount(l, legacyMedicalAmounts);
        var returnedAmount = ResolveReturnedAmount(l, enrichment);
        var reductionAmount = ResolveReductionAmount(l, enrichment);
        var toSettleAmount = ResolveToSettleAmount(l, enrichment);
        var reductionDate = ResolveReductionDate(l, enrichment);
        var paidDate = ResolvePaidDate(l, enrichment);
        var daysSincePurchase = GetDaysSincePurchase(l.PurchaseDate);
        var grossProfit = returnedAmount - (purchaseAmount ?? 0m);

        return new DIYReportRow
        {
            CaseId = l.CaseId,
            LienId = l.Id,
            CaseNumber = caseEntity?.CaseNumber ?? string.Empty,
            LienNumber = l.LienNumber,
            PlaintiffFirstName = caseEntity?.ClientFirstName ?? l.SubjectFirstName ?? string.Empty,
            PlaintiffLastName = caseEntity?.ClientLastName ?? l.SubjectLastName ?? string.Empty,
            ClientName = caseEntity is null
                ? string.Join(" ", new[] { l.SubjectFirstName, l.SubjectLastName }.Where(v => !string.IsNullOrWhiteSpace(v)))
                : $"{caseEntity.ClientFirstName} {caseEntity.ClientLastName}".Trim(),
            Status = l.Status,
            CaseStatus = caseEntity?.Status,
            DateOfLoss = caseEntity?.DateOfIncident ?? l.IncidentDate,
            PurchaseDate = l.PurchaseDate,
            DateClosed = string.Equals(l.Status, LienStatus.Settled, StringComparison.OrdinalIgnoreCase)
                ? l.ClosedAtUtc
                : null,
            InitialServiceDate = l.InitialServiceDate,
            EndServiceDate = l.EndServiceDate,
            SettlementDate = ResolveSettlementDate(l, enrichment),
            ReductionDate = reductionDate,
            FirstPurchaseDate = l.PurchaseDate,
            LastPurchaseDate = l.PurchaseDate,
            DaysSincePurchase = daysSincePurchase,
            BillingAmount = billingAmount,
            PurchaseAmount = purchaseAmount,
            ReturnedAmount = string.Equals(l.Status, LienStatus.Settled, StringComparison.OrdinalIgnoreCase)
                ? returnedAmount
                : null,
            ReductionAmount = reductionAmount,
            RemainingBillingAmount = reductionAmount > 0m
                ? billingAmount - (purchaseAmount ?? 0m)
                : null,
            ReductionPercentage = billingAmount == 0m ? 0m : reductionAmount / billingAmount * 100m,
            GrossProfit = grossProfit,
            Roi = purchaseAmount is > 0m ? grossProfit / purchaseAmount.Value * 100m : 0m,
            AnnualizedRoi = purchaseAmount is > 0m && daysSincePurchase is > 0
                ? grossProfit / purchaseAmount.Value * (365m / daysSincePurchase.Value) * 100m
                : 0m,
            LienTotal = l.CurrentBalance,
            NumberOfLiens = 1,
            ToSettleAmount = toSettleAmount,
            SettledAmount = returnedAmount,
            DaysSinceReductionApproval = GetDaysSinceReductionApproval(reductionDate, paidDate),
            MedicalFacility = enrichment.MedicalFacilityByLienId.GetValueOrDefault(l.Id, string.Empty),
            LawFirm = caseDetails.LawFirm,
            CaseType = caseDetails.CaseType,
            CaseManager = caseDetails.CaseManager,
            UccFiled = caseDetails.UccFiled,
            FeedNote = enrichment.FeedNotesByCaseId
                .GetValueOrDefault(l.CaseId ?? Guid.Empty, FeedNoteReportDetails.Empty)
                .Note,
            FeedNoteDate = enrichment.FeedNotesByCaseId
                .GetValueOrDefault(l.CaseId ?? Guid.Empty, FeedNoteReportDetails.Empty)
                .Date,
            TrackingNotes = enrichment.TrackingNotesByCaseId
                .GetValueOrDefault(l.CaseId ?? Guid.Empty, TrackingNoteReportDetails.Empty)
                .Notes,
            LastTrackingNoteDate = enrichment.TrackingNotesByCaseId
                .GetValueOrDefault(l.CaseId ?? Guid.Empty, TrackingNoteReportDetails.Empty)
                .LastDate,
            LastActivity = enrichment.CaseActivityByCaseId
                .GetValueOrDefault(l.CaseId ?? Guid.Empty, CaseActivityReportDetails.Empty)
                .Description,
            LastActivityAtUtc = enrichment.CaseActivityByCaseId
                .GetValueOrDefault(l.CaseId ?? Guid.Empty, CaseActivityReportDetails.Empty)
                .TimestampUtc,
            Extra = new Dictionary<string, object?>(),
        };
    }

    private static bool IsClosedCaseStatus(string? status) =>
        string.Equals(status, CaseStatus.Closed, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(status, CaseStatus.CaseSettled, StringComparison.OrdinalIgnoreCase);

    private static List<DIYReportRow> BuildCaseRows(
        IReadOnlyCollection<Lien> liens,
        IReadOnlyDictionary<Guid, Case> casesById,
        IReadOnlyDictionary<Guid, LegacyMedicalAmounts> legacyMedicalAmounts,
        ReportRowEnrichment enrichment,
        int page,
        int limit)
    {
        var liensByCaseId = liens
            .Where(l => l.CaseId.HasValue)
            .GroupBy(l => l.CaseId!.Value)
            .ToDictionary(group => group.Key, group => group.ToList());
        var orderedCases = casesById.Values
            .OrderByDescending(caseEntity => caseEntity.CreatedAtUtc)
            .ThenByDescending(caseEntity => caseEntity.Id)
            .ToList();
        var skip = (long)(page - 1) * limit;
        if (skip >= orderedCases.Count)
            return [];

        return orderedCases
            .Skip((int)skip)
            .Take(limit)
            .Select(caseEntity =>
            {
                var caseLiens = liensByCaseId.GetValueOrDefault(caseEntity.Id, []);
                var caseDetails = enrichment.CaseDetailsById.GetValueOrDefault(caseEntity.Id, CaseReportDetails.Empty);
                var purchaseDates = caseLiens
                    .Where(l => l.PurchaseDate.HasValue)
                    .Select(l => l.PurchaseDate!.Value)
                    .ToList();
                var reductionDates = caseLiens
                    .Select(l => ResolveReductionDate(l, enrichment))
                    .Where(date => date.HasValue)
                    .Select(date => date!.Value)
                    .ToList();
                var paidDates = caseLiens
                    .Select(l => ResolvePaidDate(l, enrichment))
                    .Where(date => date.HasValue)
                    .Select(date => date!.Value)
                    .ToList();
                var settlementDates = caseLiens
                    .Select(l => ResolveSettlementDate(l, enrichment))
                    .Where(date => date.HasValue)
                    .Select(date => date!.Value)
                    .ToList();
                var closedDates = caseLiens
                    .Where(l => string.Equals(l.Status, LienStatus.Settled, StringComparison.OrdinalIgnoreCase) &&
                                l.ClosedAtUtc.HasValue)
                    .Select(l => l.ClosedAtUtc!.Value)
                    .ToList();
                var facilities = caseLiens
                    .Select(l => enrichment.MedicalFacilityByLienId.GetValueOrDefault(l.Id, string.Empty))
                    .Where(name => !string.IsNullOrWhiteSpace(name))
                    .Distinct(StringComparer.OrdinalIgnoreCase);

                return new DIYReportRow
                {
                    CaseId = caseEntity.Id,
                    LienId = null,
                    CaseNumber = caseEntity.CaseNumber,
                    LienNumber = string.Empty,
                    PlaintiffFirstName = caseEntity.ClientFirstName,
                    PlaintiffLastName = caseEntity.ClientLastName,
                    ClientName = $"{caseEntity.ClientFirstName} {caseEntity.ClientLastName}".Trim(),
                    Status = null,
                    CaseStatus = caseEntity.Status,
                    DateOfLoss = caseEntity.DateOfIncident,
                    PurchaseDate = purchaseDates.Count > 0
                        ? purchaseDates.Min()
                        : null,
                    DateClosed = closedDates.Count > 0 ? closedDates.Max() : null,
                    InitialServiceDate = caseLiens.Count > 0 ? caseLiens.Min(l => l.InitialServiceDate) : null,
                    EndServiceDate = caseLiens.Count > 0 ? caseLiens.Max(l => l.EndServiceDate) : null,
                    SettlementDate = settlementDates.Count > 0 ? settlementDates.Max() : null,
                    ReductionDate = reductionDates.Count > 0 ? reductionDates.Max() : null,
                    FirstPurchaseDate = purchaseDates.Count > 0 ? purchaseDates.Min() : null,
                    LastPurchaseDate = purchaseDates.Count > 0 ? purchaseDates.Max() : null,
                    BillingAmount = caseLiens.Sum(l => ResolveBillingAmount(l, legacyMedicalAmounts)),
                    PurchaseAmount = caseLiens.Sum(l => ResolvePurchaseAmount(l, legacyMedicalAmounts) ?? 0m),
                    ReturnedAmount = caseLiens.Sum(l => ResolveReturnedAmount(l, enrichment)),
                    ReductionAmount = caseLiens.Sum(l => ResolveReductionAmount(l, enrichment)),
                    LienTotal = caseLiens.Sum(l => l.CurrentBalance ?? 0m),
                    NumberOfLiens = caseLiens.Count,
                    ToSettleAmount = caseLiens.Sum(l => ResolveToSettleAmount(l, enrichment)),
                    SettledAmount = caseLiens.Sum(l => ResolveReturnedAmount(l, enrichment)),
                    DaysSinceReductionApproval = GetDaysSinceReductionApproval(
                        reductionDates.Count > 0 ? reductionDates.Max() : null,
                        paidDates.Count > 0 ? paidDates.Max() : null),
                    MedicalFacility = string.Join(", ", facilities),
                    LawFirm = caseDetails.LawFirm,
                    CaseType = caseDetails.CaseType,
                    CaseManager = caseDetails.CaseManager,
                    UccFiled = caseDetails.UccFiled,
                    FeedNote = enrichment.FeedNotesByCaseId
                        .GetValueOrDefault(caseEntity.Id, FeedNoteReportDetails.Empty)
                        .Note,
                    FeedNoteDate = enrichment.FeedNotesByCaseId
                        .GetValueOrDefault(caseEntity.Id, FeedNoteReportDetails.Empty)
                        .Date,
                    TrackingNotes = enrichment.TrackingNotesByCaseId
                        .GetValueOrDefault(caseEntity.Id, TrackingNoteReportDetails.Empty)
                        .Notes,
                    LastTrackingNoteDate = enrichment.TrackingNotesByCaseId
                        .GetValueOrDefault(caseEntity.Id, TrackingNoteReportDetails.Empty)
                        .LastDate,
                    LastActivity = enrichment.CaseActivityByCaseId
                        .GetValueOrDefault(caseEntity.Id, CaseActivityReportDetails.Empty)
                        .Description,
                    LastActivityAtUtc = enrichment.CaseActivityByCaseId
                        .GetValueOrDefault(caseEntity.Id, CaseActivityReportDetails.Empty)
                        .TimestampUtc,
                    Extra = new Dictionary<string, object?>(),
                };
            })
            .ToList();
    }

    private async Task<ReportRowEnrichment> GetRowEnrichmentAsync(
        Guid tenantId,
        IReadOnlyCollection<Lien> liens,
        IReadOnlyDictionary<Guid, Case> casesById,
        IReadOnlyCollection<ServicingItem> servicingItems,
        bool includeTrackingNotes,
        bool includeCaseActivity,
        bool includeFeedNotes,
        CancellationToken ct)
    {
        var lienIds = liens.Select(lien => lien.Id).Distinct().ToList();
        var facilityInfoByLienId = servicingItems
            .Where(item => item.LienId.HasValue &&
                           string.Equals(item.TaskType, "LegacyMedicalFacilityInfo", StringComparison.Ordinal))
            .GroupBy(item => item.LienId!.Value)
            .ToDictionary(group => group.Key, group => group.OrderByDescending(item => item.CreatedAtUtc).First());

        var caseMetadataById = casesById.Values
            .ToDictionary(caseEntity => caseEntity.Id, caseEntity => ParseLegacyNoteFields(caseEntity.Notes));

        var facilityIds = new HashSet<Guid>(liens
            .Where(lien => lien.FacilityId.HasValue)
            .Select(lien => lien.FacilityId!.Value));
        var contactIds = new HashSet<Guid>();

        foreach (var fields in caseMetadataById.Values)
        {
            AddGuidIfValid(contactIds, fields.GetValueOrDefault("lawFirmId"));
            AddGuidIfValid(contactIds, fields.GetValueOrDefault("caseManagerId"));
        }

        foreach (var lien in liens)
        {
            if (facilityInfoByLienId.TryGetValue(lien.Id, out var facilityInfo))
                AddGuidIfValid(facilityIds, ParseLegacyNoteFields(facilityInfo.Notes).GetValueOrDefault("facilityId"));
        }

        contactIds.UnionWith(facilityIds);

        var facilitiesById = (await _facilityRepo.GetByIdsAsync(tenantId, facilityIds, ct))
            .ToDictionary(facility => facility.Id);
        var contactsById = (await _contactRepo.GetByIdsAsync(tenantId, contactIds, ct))
            .ToDictionary(contact => contact.Id);
        var lawFirmsByOrgId = (await _contactRepo.GetAllByTypeAsync(
                tenantId,
                ContactType.LawFirm,
                isActive: null,
                ct))
            .GroupBy(contact => contact.OrgId)
            .ToDictionary(group => group.Key, group => group.First());

        var medicalFacilityByLienId = new Dictionary<Guid, string>();
        foreach (var lien in liens)
        {
            var facilityFields = facilityInfoByLienId.TryGetValue(lien.Id, out var facilityInfo)
                ? ParseLegacyNoteFields(facilityInfo.Notes)
                : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var facilityId = FirstGuid(
                facilityFields.GetValueOrDefault("facilityId"),
                lien.FacilityId?.ToString());

            medicalFacilityByLienId[lien.Id] = FirstNonEmpty(
                facilityFields.GetValueOrDefault("facilityName"),
                facilityId.HasValue && facilitiesById.TryGetValue(facilityId.Value, out var facility)
                    ? facility.Name
                    : null,
                facilityId.HasValue && contactsById.TryGetValue(facilityId.Value, out var facilityContact)
                    ? DisplayContactName(facilityContact)
                    : null) ?? string.Empty;
        }

        var caseDetailsById = new Dictionary<Guid, CaseReportDetails>();
        foreach (var caseEntity in casesById.Values)
        {
            var fields = caseMetadataById[caseEntity.Id];
            var lawFirmId = FirstGuid(fields.GetValueOrDefault("lawFirmId"));
            var caseManagerId = FirstGuid(fields.GetValueOrDefault("caseManagerId"));

            caseDetailsById[caseEntity.Id] = new CaseReportDetails(
                FirstNonEmpty(
                    lawFirmId.HasValue && contactsById.TryGetValue(lawFirmId.Value, out var lawFirm)
                        ? DisplayContactName(lawFirm)
                        : null,
                    fields.GetValueOrDefault("lawFirm"),
                    lawFirmsByOrgId.TryGetValue(caseEntity.OrgId, out var organizationLawFirm)
                        ? DisplayContactName(organizationLawFirm)
                        : null) ?? string.Empty,
                FirstNonEmpty(
                    caseManagerId.HasValue && contactsById.TryGetValue(caseManagerId.Value, out var caseManager)
                        ? DisplayPersonName(caseManager)
                        : null,
                    fields.GetValueOrDefault("caseManager")) ?? string.Empty,
                FirstNonEmpty(
                    fields.GetValueOrDefault("accidentType"),
                    fields.GetValueOrDefault("caseType")) ?? string.Empty,
                NormalizeYesNoFlag(
                    fields.GetValueOrDefault("isUccFiled"),
                    fields.GetValueOrDefault("isUCCFiled")));
        }

        var reductions = await _reductionRepo.GetByLienIdsAsync(tenantId, lienIds, ct);
        var reductionAmountsByLienId = reductions
            .GroupBy(reduction => reduction.LienId)
            .ToDictionary(
                group => group.Key,
                group => new ReductionReportAmounts(
                    group.Sum(reduction => reduction.Amount),
                    group.Max(reduction => reduction.ReductionDate)));

        var settlementAmountsByLienId = (await _settlementRepo.GetByLienIdsAsync(tenantId, lienIds, ct))
            .GroupBy(settlement => settlement.LienId)
            .ToDictionary(group => group.Key, BuildSettlementReportAmounts);

        var paymentDetails = await _paymentDetailRepo.GetByLienIdsAsync(tenantId, lienIds, ct);
        var returnedAmountsByLienId = paymentDetails
            .GroupBy(payment => payment.LienId)
            .ToDictionary(group => group.Key, group => group.Sum(payment => payment.Amount));
        var paidDatesByLienId = paymentDetails
            .Where(payment => payment.PaymentDate.HasValue)
            .GroupBy(payment => payment.LienId)
            .ToDictionary(group => group.Key, group => group.Max(payment => payment.PaymentDate!.Value));

        var trackingNotesByCaseId = new Dictionary<Guid, TrackingNoteReportDetails>();
        if (includeTrackingNotes)
        {
            var trackingNotes = await _caseNoteRepo.GetTrackingByCaseIdsAsync(
                tenantId,
                casesById.Keys.ToList(),
                ct);
            trackingNotesByCaseId = trackingNotes
                .Where(note => !string.IsNullOrWhiteSpace(note.Content))
                .GroupBy(note => note.CaseId)
                .ToDictionary(
                    group => group.Key,
                    group =>
                    {
                        var orderedNotes = group
                            .OrderByDescending(note => note.CreatedAtUtc)
                            .ThenByDescending(note => note.Id)
                            .ToList();
                        return new TrackingNoteReportDetails(
                            string.Join("\n", orderedNotes.Select(note => note.Content)),
                            DateOnly.FromDateTime(orderedNotes[0].CreatedAtUtc));
                    });
        }

        var feedNotesByCaseId = new Dictionary<Guid, FeedNoteReportDetails>();
        if (includeFeedNotes)
        {
            var feedNotes = await _caseNoteRepo.GetLatestFeedByCaseIdsAsync(
                tenantId,
                casesById.Keys.ToList(),
                ct);
            feedNotesByCaseId = feedNotes.ToDictionary(
                note => note.CaseId,
                note => new FeedNoteReportDetails(
                    note.Content,
                    DateOnly.FromDateTime(note.CreatedAtUtc)));
        }

        var caseActivityByCaseId = new Dictionary<Guid, CaseActivityReportDetails>();
        if (includeCaseActivity)
        {
            var caseUpdates = await _caseNoteRepo.GetLatestCaseUpdatesByCaseIdsAsync(
                tenantId,
                casesById.Keys.ToList(),
                ct);
            caseActivityByCaseId = caseUpdates.ToDictionary(
                note => note.CaseId,
                note => new CaseActivityReportDetails(
                    LegacyCaseUpdateCompatibility.NormalizeDescription(note.Content, note.Category),
                    note.UpdatedAtUtc ?? note.CreatedAtUtc));
        }

        return new ReportRowEnrichment(
            medicalFacilityByLienId,
            caseDetailsById,
            reductionAmountsByLienId,
            settlementAmountsByLienId,
            returnedAmountsByLienId,
            paidDatesByLienId,
            trackingNotesByCaseId,
            caseActivityByCaseId,
            feedNotesByCaseId);
    }

    private static bool ShouldIncludeTrackingNotes(DIYReportRunRequest request)
    {
        if (!HasReportProperty(request, "columns", out var columns) ||
            columns.ValueKind != JsonValueKind.Array ||
            columns.GetArrayLength() == 0)
        {
            return true;
        }

        return columns.EnumerateArray().Any(column =>
        {
            var key = column.ValueKind switch
            {
                JsonValueKind.String => column.GetString(),
                JsonValueKind.Object when column.TryGetProperty("key", out var value) &&
                                          value.ValueKind == JsonValueKind.String => value.GetString(),
                JsonValueKind.Object when column.TryGetProperty("name", out var value) &&
                                          value.ValueKind == JsonValueKind.String => value.GetString(),
                _ => null,
            };
            return string.Equals(key, "last_case_note", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(key, "last_case_note_date", StringComparison.OrdinalIgnoreCase);
        });
    }

    private static bool ShouldIncludeFeedNotes(DIYReportRunRequest request)
    {
        if (!HasReportProperty(request, "columns", out var columns) ||
            columns.ValueKind != JsonValueKind.Array ||
            columns.GetArrayLength() == 0)
        {
            return true;
        }

        return columns.EnumerateArray().Any(column =>
        {
            var key = column.ValueKind switch
            {
                JsonValueKind.String => column.GetString(),
                JsonValueKind.Object when column.TryGetProperty("key", out var value) &&
                                          value.ValueKind == JsonValueKind.String => value.GetString(),
                JsonValueKind.Object when column.TryGetProperty("name", out var value) &&
                                          value.ValueKind == JsonValueKind.String => value.GetString(),
                _ => null,
            };
            return string.Equals(key, "notes", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(key, "notes_date", StringComparison.OrdinalIgnoreCase);
        });
    }

    private static bool ShouldIncludeCaseActivity(DIYReportRunRequest request)
    {
        if (!HasReportProperty(request, "columns", out var columns) ||
            columns.ValueKind != JsonValueKind.Array ||
            columns.GetArrayLength() == 0)
        {
            return true;
        }

        return columns.EnumerateArray().Any(column =>
        {
            var key = column.ValueKind switch
            {
                JsonValueKind.String => column.GetString(),
                JsonValueKind.Object when column.TryGetProperty("key", out var value) &&
                                          value.ValueKind == JsonValueKind.String => value.GetString(),
                JsonValueKind.Object when column.TryGetProperty("name", out var value) &&
                                          value.ValueKind == JsonValueKind.String => value.GetString(),
                _ => null,
            };
            return string.Equals(key, "last_case_tracking_note", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(key, "last_case_tracking_date", StringComparison.OrdinalIgnoreCase);
        });
    }

    private static Dictionary<string, string> ParseLegacyNoteFields(string? notes)
    {
        var fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(notes))
            return fields;

        const string legacyMetadataMarker = "[legacy-meta]";
        var rawMetadata = notes;
        var markerIndex = notes.IndexOf(legacyMetadataMarker, StringComparison.Ordinal);
        if (markerIndex >= 0)
            rawMetadata = notes[(markerIndex + legacyMetadataMarker.Length)..].Trim();

        foreach (var segment in rawMetadata.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var separator = segment.IndexOf('=');
            if (separator <= 0)
                continue;

            var key = segment[..separator].Trim();
            var value = segment[(separator + 1)..].Trim();
            if (!string.IsNullOrWhiteSpace(key))
                fields[key] = value;
        }

        return fields;
    }

    private static void AddGuidIfValid(ISet<Guid> target, string? value)
    {
        if (Guid.TryParse(value, out var id))
            target.Add(id);
    }

    private static Guid? FirstGuid(params string?[] values)
        => values
            .Select(value => Guid.TryParse(value, out var id) ? (Guid?)id : null)
            .FirstOrDefault(id => id.HasValue);

    private static string? FirstNonEmpty(params string?[] values)
        => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim();

    private static string NormalizeYesNoFlag(params string?[] values)
    {
        var value = FirstNonEmpty(values);
        if (string.IsNullOrWhiteSpace(value))
            return "No";

        return value.ToUpperInvariant() switch
        {
            "TRUE" or "YES" or "Y" or "1" => "Yes",
            _ => "No",
        };
    }

    private static string DisplayContactName(Contact contact)
        => FirstNonEmpty(contact.Organization, contact.DisplayName) ?? string.Empty;

    private static string DisplayPersonName(Contact contact)
        => FirstNonEmpty(contact.DisplayName, contact.Organization) ?? string.Empty;

    private static SettlementReportAmounts BuildSettlementReportAmounts(
        IGrouping<Guid, LienSettlement> settlements)
    {
        var reductionAmount = 0m;
        var returnedAmount = 0m;
        var hasLegacyReductionAmount = false;
        var hasLegacyReturnedAmount = false;
        DateOnly? reductionDate = null;
        var reductionSettlementDates = new List<DateOnly>();

        foreach (var settlement in settlements)
        {
            var fields = ParseLegacyNoteFields(settlement.Note);
            if (fields.TryGetValue("reductionAmount", out var reductionValue))
            {
                hasLegacyReductionAmount = true;
                if (settlement.SettlementDate.HasValue)
                    reductionSettlementDates.Add(settlement.SettlementDate.Value);

                if (decimal.TryParse(reductionValue, NumberStyles.Any, CultureInfo.InvariantCulture, out var amount))
                    reductionAmount += amount;
            }

            if (fields.TryGetValue("totalSettledAmount", out var returnedValue))
            {
                hasLegacyReturnedAmount = true;
                if (decimal.TryParse(returnedValue, NumberStyles.Any, CultureInfo.InvariantCulture, out var amount))
                    returnedAmount += amount;
            }

            if (fields.TryGetValue("reductionDate", out var reductionDateValue) &&
                TryParseLegacyDate(reductionDateValue, out var parsedReductionDate) &&
                (!reductionDate.HasValue || parsedReductionDate > reductionDate.Value))
            {
                reductionDate = parsedReductionDate;
            }
        }

        var settlementDates = settlements
            .Where(settlement => settlement.SettlementDate.HasValue)
            .Select(settlement => settlement.SettlementDate!.Value)
            .ToList();

        return new SettlementReportAmounts(
            settlements.Sum(settlement => settlement.Amount),
            reductionAmount,
            returnedAmount,
            hasLegacyReductionAmount,
            hasLegacyReturnedAmount,
            settlementDates.Count > 0 ? settlementDates.Max() : null,
            reductionDate ?? (reductionSettlementDates.Count > 0 ? reductionSettlementDates.Max() : null));
    }

    private static bool TryParseLegacyDate(string? value, out DateOnly date)
    {
        var formats = new[] { "yyyy-MM-dd", "M/d/yyyy", "MM/dd/yyyy" };
        return DateOnly.TryParseExact(
                   value,
                   formats,
                   CultureInfo.InvariantCulture,
                   DateTimeStyles.None,
                   out date) ||
               DateOnly.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out date);
    }

    private static decimal ResolveReturnedAmount(
        Lien lien,
        ReportRowEnrichment enrichment)
    {
        if (enrichment.SettlementAmountsByLienId.TryGetValue(lien.Id, out var settlementAmounts) &&
            settlementAmounts.HasLegacyReturnedAmount)
        {
            return settlementAmounts.ReturnedAmount;
        }

        return lien.PayoffAmount ??
               (enrichment.ReturnedAmountsByLienId.TryGetValue(lien.Id, out var returnedAmount)
                   ? returnedAmount
                   : 0m);
    }

    private static decimal ResolveReductionAmount(Lien lien, ReportRowEnrichment enrichment)
    {
        if (enrichment.SettlementAmountsByLienId.TryGetValue(lien.Id, out var settlementAmounts) &&
            settlementAmounts.HasLegacyReductionAmount)
        {
            return settlementAmounts.ReductionAmount;
        }

        return enrichment.ReductionAmountsByLienId.TryGetValue(lien.Id, out var reductionAmounts)
            ? reductionAmounts.Amount
            : 0m;
    }

    private static decimal ResolveToSettleAmount(Lien lien, ReportRowEnrichment enrichment) =>
        enrichment.SettlementAmountsByLienId.TryGetValue(lien.Id, out var settlementAmounts)
            ? settlementAmounts.ToSettleAmount
            : 0m;

    private static DateOnly? ResolveSettlementDate(Lien lien, ReportRowEnrichment enrichment) =>
        enrichment.SettlementAmountsByLienId.TryGetValue(lien.Id, out var settlementAmounts)
            ? settlementAmounts.SettlementDate
            : null;

    private static DateOnly? ResolveReductionDate(Lien lien, ReportRowEnrichment enrichment)
    {
        if (enrichment.SettlementAmountsByLienId.TryGetValue(lien.Id, out var settlementAmounts) &&
            settlementAmounts.ReductionDate.HasValue)
        {
            return settlementAmounts.ReductionDate;
        }

        return enrichment.ReductionAmountsByLienId.TryGetValue(lien.Id, out var reductionAmounts)
            ? reductionAmounts.ReductionDate
            : null;
    }

    private static DateOnly? ResolvePaidDate(Lien lien, ReportRowEnrichment enrichment)
    {
        if (enrichment.PaidDatesByLienId.TryGetValue(lien.Id, out var paidDate))
            return paidDate;

        return string.Equals(lien.Status, LienStatus.Settled, StringComparison.OrdinalIgnoreCase) &&
               lien.ClosedAtUtc.HasValue
            ? DateOnly.FromDateTime(lien.ClosedAtUtc.Value)
            : null;
    }

    private static int? GetDaysSincePurchase(DateOnly? purchaseDate) =>
        purchaseDate.HasValue
            ? Math.Max(DateOnly.FromDateTime(DateTime.UtcNow).DayNumber - purchaseDate.Value.DayNumber, 0)
            : null;

    private static int? GetDaysSinceReductionApproval(DateOnly? reductionDate, DateOnly? paidDate) =>
        reductionDate.HasValue && paidDate.HasValue
            ? Math.Max(paidDate.Value.DayNumber - reductionDate.Value.DayNumber, 0)
            : null;

    private sealed record CaseReportDetails(string LawFirm, string CaseManager, string CaseType, string UccFiled)
    {
        public static readonly CaseReportDetails Empty = new(string.Empty, string.Empty, string.Empty, "No");
    }

    private sealed record TrackingNoteReportDetails(string Notes, DateOnly? LastDate)
    {
        public static readonly TrackingNoteReportDetails Empty = new(string.Empty, null);
    }

    private sealed record FeedNoteReportDetails(string Note, DateOnly? Date)
    {
        public static readonly FeedNoteReportDetails Empty = new(string.Empty, null);
    }

    private sealed record CaseActivityReportDetails(string Description, DateTime? TimestampUtc)
    {
        public static readonly CaseActivityReportDetails Empty = new(string.Empty, null);
    }

    private sealed record ReportRowEnrichment(
        IReadOnlyDictionary<Guid, string> MedicalFacilityByLienId,
        IReadOnlyDictionary<Guid, CaseReportDetails> CaseDetailsById,
        IReadOnlyDictionary<Guid, ReductionReportAmounts> ReductionAmountsByLienId,
        IReadOnlyDictionary<Guid, SettlementReportAmounts> SettlementAmountsByLienId,
        IReadOnlyDictionary<Guid, decimal> ReturnedAmountsByLienId,
        IReadOnlyDictionary<Guid, DateOnly> PaidDatesByLienId,
        IReadOnlyDictionary<Guid, TrackingNoteReportDetails> TrackingNotesByCaseId,
        IReadOnlyDictionary<Guid, CaseActivityReportDetails> CaseActivityByCaseId,
        IReadOnlyDictionary<Guid, FeedNoteReportDetails> FeedNotesByCaseId);

    private readonly record struct ReductionReportAmounts(decimal Amount, DateOnly ReductionDate);

    private readonly record struct SettlementReportAmounts(
        decimal ToSettleAmount,
        decimal ReductionAmount,
        decimal ReturnedAmount,
        bool HasLegacyReductionAmount,
        bool HasLegacyReturnedAmount,
        DateOnly? SettlementDate,
        DateOnly? ReductionDate);

    private static bool HasReportProperty(DIYReportRunRequest request, string propertyName, out JsonElement value)
    {
        if (request.Config.ValueKind == JsonValueKind.Object &&
            request.Config.TryGetProperty(propertyName, out value))
        {
            return true;
        }

        if (request.ExtensionData is not null &&
            request.ExtensionData.TryGetValue(propertyName, out value))
        {
            return true;
        }

        value = default;
        return false;
    }

    private static int GetReportInt(DIYReportRunRequest request, string propertyName, int fallback)
    {
        if (!HasReportProperty(request, propertyName, out var value))
            return fallback;

        return value.ValueKind switch
        {
            JsonValueKind.Number when value.TryGetInt32(out var number) => number,
            JsonValueKind.String when int.TryParse(value.GetString(), out var number) => number,
            _ => fallback,
        };
    }

    private static string? GetReportString(DIYReportRunRequest request, string propertyName)
    {
        if (!HasReportProperty(request, propertyName, out var value) ||
            value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return null;
        }

        if (value.ValueKind == JsonValueKind.String)
            return value.GetString();

        if (value.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in value.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(item.GetString()))
                    return item.GetString();
            }
        }

        return null;
    }

    private static DateOnly? GetReportDateOnly(DIYReportRunRequest request, string propertyName)
    {
        var value = GetReportString(request, propertyName);
        if (string.IsNullOrWhiteSpace(value))
            return null;

        return DateOnly.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            ? date
            : null;
    }

    private static DateTime? GetReportDateTime(DIYReportRunRequest request, string propertyName, bool endOfDay)
    {
        var value = GetReportString(request, propertyName);
        if (string.IsNullOrWhiteSpace(value))
            return null;

        if (!DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            return null;

        return endOfDay ? date.Date.AddDays(1).AddTicks(-1) : date.Date;
    }

    private static IReadOnlyCollection<Guid> GetReportGuidList(DIYReportRunRequest request, string propertyName)
    {
        if (!HasReportProperty(request, propertyName, out var value) ||
            value.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return value.EnumerateArray()
            .Select(item => item.ValueKind == JsonValueKind.String ? item.GetString() : null)
            .Where(v => Guid.TryParse(v, out _))
            .Select(v => Guid.Parse(v!))
            .ToList();
    }

    private static string? NormalizeDIYBulkFilter(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        return value.Trim().ToUpperInvariant() switch
        {
            // The legacy report UI always submits N when its bulk checkbox is
            // unchecked. The legacy SQL treated that sentinel as no filter.
            "N" => null,
            "Y" or "YES" or "TRUE" or "1" => "Yes",
            "NO" or "FALSE" or "0" => "No",
            _ => value.Trim(),
        };
    }

    private static bool CanIncludeUnlinkedCases(
        bool isCasesReport,
        IReadOnlyCollection<string> lienStatuses,
        DateOnly? purchaseDateFrom,
        DateOnly? purchaseDateTo,
        DateTime? closedDateFrom,
        DateTime? closedDateTo,
        string? normalizedBulkFilter,
        ReportRelationshipFilters filters) =>
        isCasesReport &&
        lienStatuses.Count == 0 &&
        !purchaseDateFrom.HasValue &&
        !purchaseDateTo.HasValue &&
        !closedDateFrom.HasValue &&
        !closedDateTo.HasValue &&
        string.IsNullOrWhiteSpace(normalizedBulkFilter) &&
        filters.FundingCompanyIds.Count == 0 &&
        filters.MedicalFacilityIds.Count == 0 &&
        filters.MedicalProviderIds.Count == 0;

    private static DIYReportSummaryTotals BuildSummary(
        IReadOnlyCollection<Lien> liens,
        IReadOnlyDictionary<Guid, LegacyMedicalAmounts> legacyMedicalAmounts,
        ReportRowEnrichment enrichment,
        IReadOnlyDictionary<Guid, Case> casesById,
        bool isCombinedReport,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var totalPurchase = liens.Sum(l => ResolvePurchaseAmount(l, legacyMedicalAmounts) ?? 0m);
        var totalBilling = liens.Sum(l => ResolveBillingAmount(l, legacyMedicalAmounts));
        var totalReturned = liens.Sum(l => ResolveReturnedAmount(l, enrichment));
        var totalAmtToSettle = isCombinedReport
            ? liens.Sum(lien => ResolveToSettleAmount(lien, enrichment))
            : totalBilling - totalReturned;
        var totalGrossProfit = totalReturned - totalPurchase;
        var roiValues = liens
            .Select(lien => new
            {
                Purchase = ResolvePurchaseAmount(lien, legacyMedicalAmounts) ?? 0m,
                Returned = ResolveReturnedAmount(lien, enrichment),
            })
            .Where(amounts => amounts.Purchase > 0m)
            .Select(amounts => (amounts.Returned - amounts.Purchase) / amounts.Purchase * 100m)
            .ToList();
        var avgRoi = roiValues.Count == 0 ? 0m : roiValues.Average();
        var openLiens = liens.Count(lien => LienStatus.Open.Contains(lien.Status));
        var closedLiens = liens.Count(lien =>
            string.Equals(lien.Status, LienStatus.Settled, StringComparison.OrdinalIgnoreCase));
        var uniqueCaseIds = casesById.Keys.ToList();
        var closedCases = 0;

        foreach (var caseId in uniqueCaseIds)
        {
            ct.ThrowIfCancellationRequested();
            if (casesById.TryGetValue(caseId, out var caseEntity) &&
                IsClosedCaseStatus(caseEntity.Status))
                closedCases++;
        }

        return new DIYReportSummaryTotals
        {
            TotalCases = uniqueCaseIds.Count,
            TotalLiens = liens.Count,
            TotalPurchaseAmt = totalPurchase,
            TotalBillingAmt = totalBilling,
            TotalAmtToSettle = totalAmtToSettle,
            TotalReturnedAmt = totalReturned,
            TotalGrossProfit = totalGrossProfit,
            AvgRoi = avgRoi,
            TotalOpenCases = Math.Max(uniqueCaseIds.Count - closedCases, 0),
            TotalClosedCases = closedCases,
            TotalOpenLiens = openLiens,
            TotalClosedLiens = closedLiens,
        };
    }

    private static IReadOnlyDictionary<Guid, LegacyMedicalAmounts> GetLegacyMedicalAmountsByLienId(
        IReadOnlyCollection<Lien> liens,
        IReadOnlyCollection<ServicingItem> servicingItems)
    {
        var lienIds = liens.Select(lien => lien.Id).ToHashSet();

        return servicingItems
            .Where(item => item.LienId.HasValue &&
                           lienIds.Contains(item.LienId.Value) &&
                           string.Equals(item.TaskType, "LegacyMedicalCode", StringComparison.Ordinal))
            .GroupBy(item => item.LienId!.Value)
            .ToDictionary(
                group => group.Key,
                group => group.Aggregate(
                    new LegacyMedicalAmounts(),
                    (amounts, item) => amounts.Add(ParseLegacyMedicalAmounts(item.Notes))));
    }

    private static LegacyMedicalAmounts ParseLegacyMedicalAmounts(string? notes)
    {
        var amounts = new LegacyMedicalAmounts();
        if (string.IsNullOrWhiteSpace(notes))
            return amounts;

        foreach (var segment in notes.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var separator = segment.IndexOf('=');
            if (separator <= 0)
                continue;

            var key = segment[..separator].Trim();
            var value = segment[(separator + 1)..].Trim();
            if (!decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var amount))
                continue;

            if (string.Equals(key, "purchaseAmount", StringComparison.Ordinal))
                amounts = amounts with { PurchaseAmount = amount, HasPurchaseAmount = true };
            else if (string.Equals(key, "billingAmount", StringComparison.Ordinal))
                amounts = amounts with { BillingAmount = amount, HasBillingAmount = true };
        }

        return amounts;
    }

    private static decimal ResolveBillingAmount(
        Lien lien,
        IReadOnlyDictionary<Guid, LegacyMedicalAmounts> legacyMedicalAmounts) =>
        legacyMedicalAmounts.TryGetValue(lien.Id, out var amounts) && amounts.HasBillingAmount
            ? amounts.BillingAmount
            : lien.OriginalAmount;

    private static decimal? ResolvePurchaseAmount(
        Lien lien,
        IReadOnlyDictionary<Guid, LegacyMedicalAmounts> legacyMedicalAmounts) =>
        legacyMedicalAmounts.TryGetValue(lien.Id, out var amounts) && amounts.HasPurchaseAmount
            ? amounts.PurchaseAmount
            : lien.PurchasePrice ?? 0m;

    private readonly record struct LegacyMedicalAmounts(
        decimal PurchaseAmount = 0m,
        bool HasPurchaseAmount = false,
        decimal BillingAmount = 0m,
        bool HasBillingAmount = false)
    {
        public LegacyMedicalAmounts Add(LegacyMedicalAmounts other) => new(
            PurchaseAmount + other.PurchaseAmount,
            HasPurchaseAmount || other.HasPurchaseAmount,
            BillingAmount + other.BillingAmount,
            HasBillingAmount || other.HasBillingAmount);
    }

    private static DIYReportConfigResponse Map(DIYReportConfig r)
    {
        JsonElement config;
        try
        {
            config = JsonDocument.Parse(r.ConfigJson).RootElement;
        }
        catch
        {
            config = JsonDocument.Parse("{}").RootElement;
        }

        var reportType = GetString(config, "reportType") ?? "LIENS";
        var reportConfig = config;
        var columnCount = CountColumns(config);

        return new DIYReportConfigResponse
        {
            Id           = r.Id,
            TenantId     = r.TenantId,
            UserId       = r.UserId,
            Name         = r.Name,
            Config       = config,
            CreatedAtUtc = r.CreatedAtUtc,
            UpdatedAtUtc = r.UpdatedAtUtc,
            ReportId     = r.Id.ToString(),
            ReportName   = r.Name,
            ReportDescription = null,
            ReportType   = reportType,
            CreatedAt    = FormatLegacyDate(r.CreatedAtUtc),
            CreatedBy    = string.Empty,
            UpdatedAt    = FormatLegacyDate(r.UpdatedAtUtc),
            ReportConfig = reportConfig,
            ColumnCount  = columnCount,
        };
    }

    private static string BuildPersistedConfigJson(SaveDIYReportRequest request)
    {
        var node = request.Config.ValueKind == JsonValueKind.Object
            ? JsonNode.Parse(request.Config.GetRawText()) as JsonObject ?? new JsonObject()
            : new JsonObject();

        if (request.ExtensionData is not null)
        {
            foreach (var (key, value) in request.ExtensionData)
            {
                if (string.Equals(key, "name", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(key, "config", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(key, "description", StringComparison.OrdinalIgnoreCase))
                    continue;

                node[key] = JsonNode.Parse(value.GetRawText());
            }
        }

        return node.ToJsonString();
    }

    private static int CountColumns(JsonElement config)
    {
        if (config.ValueKind != JsonValueKind.Object ||
            !config.TryGetProperty("columns", out var columns) ||
            columns.ValueKind != JsonValueKind.Array)
        {
            return 0;
        }

        return columns.GetArrayLength();
    }

    private static string? GetString(JsonElement element, string propertyName)
    {
        if (element.ValueKind == JsonValueKind.Object &&
            element.TryGetProperty(propertyName, out var property) &&
            property.ValueKind == JsonValueKind.String)
        {
            return property.GetString();
        }

        return null;
    }

    private static string FormatLegacyDate(DateTime value) =>
        value.ToString("MM/dd/yyyy", System.Globalization.CultureInfo.InvariantCulture);
}
