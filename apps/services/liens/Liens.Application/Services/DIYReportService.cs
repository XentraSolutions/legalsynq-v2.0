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
    private readonly ICompanyRepository         _companyRepo;
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
        ICompanyRepository companyRepo,
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
        _companyRepo       = companyRepo;
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
        var medicalFacilityIds = await ResolveLienFacilityIdsAsync(
            tenantId,
            GetReportGuidList(request, "medicalFacilityIds"),
            ct);
        var relationshipFilters = new ReportRelationshipFilters(
            GetReportGuidList(request, "lawFirmIds").ToHashSet(),
            GetReportGuidList(request, "attorneyIds").ToHashSet(),
            GetReportGuidList(request, "fundingCompanyIds").ToHashSet(),
            medicalFacilityIds.ToHashSet(),
            GetReportGuidList(request, "caseManagerIds").ToHashSet(),
            GetReportGuidList(request, "medicalProviderIds").ToHashSet());

        var page = GetReportInt(request, "page", request.Page);
        if (page < 1) page = 1;
        var limit = GetReportInt(request, "limit", request.Limit);
        if (limit < 1) limit = 50;

        var reportLiens = await _lienRepo.SearchReportAsync(
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
            ct);

        var caseIds = reportLiens
            .Select(l => l.CaseId)
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Distinct()
            .ToList();
        var casesById = (await _caseRepo.GetByIdsAsync(tenantId, caseIds, ct))
            .ToDictionary(caseEntity => caseEntity.Id);
        var servicingItems = await _servicingItemRepo.GetByLienIdsAsync(
            tenantId,
            reportLiens.Select(lien => lien.Id).Distinct().ToList(),
            ["LegacyMedicalCode", "LegacyMedicalFacilityInfo"],
            ct);
        var filteredLiens = ApplyRelationshipFilters(
            reportLiens,
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
            ? TakeCasePage(reportCasesById, effectivePage, effectiveLimit)
            : pageLiens
                .Where(lien => lien.CaseId.HasValue && casesById.ContainsKey(lien.CaseId.Value))
                .Select(lien => lien.CaseId!.Value)
                .Distinct()
                .ToDictionary(caseId => caseId, caseId => casesById[caseId]);

        var legacyMedicalAmounts = GetLegacyMedicalAmountsByLienId(filteredLiens, servicingItems);
        var rowLiens = isCasesReport
            ? filteredLiens
                .Where(lien => lien.CaseId.HasValue && rowCasesById.ContainsKey(lien.CaseId.Value))
                .ToList()
            : pageLiens;
        var rowEnrichment = await GetRowEnrichmentAsync(
            tenantId,
            filteredLiens,
            rowLiens,
            rowCasesById,
            servicingItems,
            ShouldIncludeTrackingNotes(request),
            ShouldIncludeCaseActivity(request),
            ShouldIncludeFeedNotes(request),
            ct);

        var rows = isCasesReport
            ? BuildCaseRows(filteredLiens, rowCasesById, legacyMedicalAmounts, rowEnrichment)
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

    private async Task<IReadOnlyCollection<Guid>> ResolveLienFacilityIdsAsync(
        Guid tenantId,
        IReadOnlyCollection<Guid> requestedIds,
        CancellationToken ct)
    {
        if (requestedIds.Count == 0)
            return [];

        var facilityIds = requestedIds.ToHashSet();
        var contacts = await _contactRepo.GetByIdsAsync(tenantId, requestedIds, ct);
        foreach (var contact in contacts)
        {
            if (contact.FacilityId.HasValue)
                facilityIds.Add(contact.FacilityId.Value);
        }

        return facilityIds;
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

    private static Dictionary<Guid, Case> TakeCasePage(
        IReadOnlyDictionary<Guid, Case> casesById,
        int page,
        int limit)
    {
        var skip = (long)(page - 1) * limit;
        if (skip >= casesById.Count)
            return [];

        return casesById.Values
            .OrderByDescending(caseEntity => caseEntity.CreatedAtUtc)
            .ThenByDescending(caseEntity => caseEntity.Id)
            .Skip((int)skip)
            .Take(limit)
            .ToDictionary(caseEntity => caseEntity.Id);
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
        var medicalDetails = enrichment.MedicalDetailsByLienId.GetValueOrDefault(l.Id, MedicalLienReportDetails.Empty);
        var address = ResolveClientAddress(caseEntity);
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
            MedicalFacility = medicalDetails.FacilityName,
            MedicalFacilityContact = medicalDetails.FacilityContact,
            MedicalFacilityAddress = medicalDetails.FacilityAddress,
            MedicalFacilityCity = medicalDetails.FacilityCity,
            MedicalFacilityState = medicalDetails.FacilityState,
            MedicalFacilityZip = medicalDetails.FacilityZip,
            MedicalProvider = medicalDetails.Provider,
            MedicalCodes = medicalDetails.Codes,
            LawFirm = caseDetails.LawFirm.Name,
            LawFirmAddress = caseDetails.LawFirm.Address,
            LawFirmCity = caseDetails.LawFirm.City,
            LawFirmState = caseDetails.LawFirm.State,
            LawFirmZip = caseDetails.LawFirm.Zip,
            LawFirmPhone = caseDetails.LawFirm.Phone,
            LawFirmEmail = caseDetails.LawFirm.Email,
            CaseType = caseDetails.CaseType,
            CaseManager = caseDetails.CaseManager.Name,
            CaseManagerEmail = caseDetails.CaseManager.Email,
            Attorney = caseDetails.Attorney.Name,
            AttorneyPhone = caseDetails.Attorney.Phone,
            AttorneyEmail = caseDetails.Attorney.Email,
            StateOfIncident = FirstNonEmpty(caseDetails.StateOfIncident, l.Jurisdiction) ?? string.Empty,
            MedicalStatus = caseDetails.MedicalStatus,
            TrackingFollowUpDate = caseDetails.TrackingFollowUpDate,
            PlaintiffDob = caseEntity?.ClientDob,
            PlaintiffPhone = caseEntity?.ClientPhone ?? string.Empty,
            PlaintiffEmail = caseEntity?.ClientEmail ?? string.Empty,
            PlaintiffAddress = address.Address,
            PlaintiffCity = address.City,
            PlaintiffState = address.State,
            PlaintiffZip = address.Zip,
            CaseEnteredBy = caseDetails.CaseEnteredBy,
            CaseDropped = caseDetails.CaseDropped,
            MinorComp = caseDetails.MinorComp,
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
        ReportRowEnrichment enrichment)
    {
        var liensByCaseId = liens
            .Where(l => l.CaseId.HasValue)
            .GroupBy(l => l.CaseId!.Value)
            .ToDictionary(group => group.Key, group => group.ToList());
        var orderedCases = casesById.Values
            .OrderByDescending(caseEntity => caseEntity.CreatedAtUtc)
            .ThenByDescending(caseEntity => caseEntity.Id)
            .ToList();
        return orderedCases
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
                var medicalDetails = caseLiens
                    .Select(l => enrichment.MedicalDetailsByLienId.GetValueOrDefault(l.Id, MedicalLienReportDetails.Empty))
                    .ToList();
                var facilities = medicalDetails
                    .Select(details => details.FacilityName)
                    .Where(name => !string.IsNullOrWhiteSpace(name))
                    .Distinct(StringComparer.OrdinalIgnoreCase);
                var providers = medicalDetails
                    .Select(details => details.Provider)
                    .Where(name => !string.IsNullOrWhiteSpace(name))
                    .Distinct(StringComparer.OrdinalIgnoreCase);
                var codes = medicalDetails
                    .SelectMany(details => details.Codes.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                    .Distinct(StringComparer.OrdinalIgnoreCase);
                var firstMedicalDetails = medicalDetails.FirstOrDefault(details => !string.IsNullOrWhiteSpace(details.FacilityName))
                    ?? MedicalLienReportDetails.Empty;
                var address = ResolveClientAddress(caseEntity);

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
                    MedicalFacilityContact = firstMedicalDetails.FacilityContact,
                    MedicalFacilityAddress = firstMedicalDetails.FacilityAddress,
                    MedicalFacilityCity = firstMedicalDetails.FacilityCity,
                    MedicalFacilityState = firstMedicalDetails.FacilityState,
                    MedicalFacilityZip = firstMedicalDetails.FacilityZip,
                    MedicalProvider = string.Join(", ", providers),
                    MedicalCodes = string.Join(", ", codes),
                    LawFirm = caseDetails.LawFirm.Name,
                    LawFirmAddress = caseDetails.LawFirm.Address,
                    LawFirmCity = caseDetails.LawFirm.City,
                    LawFirmState = caseDetails.LawFirm.State,
                    LawFirmZip = caseDetails.LawFirm.Zip,
                    LawFirmPhone = caseDetails.LawFirm.Phone,
                    LawFirmEmail = caseDetails.LawFirm.Email,
                    CaseType = caseDetails.CaseType,
                    CaseManager = caseDetails.CaseManager.Name,
                    CaseManagerEmail = caseDetails.CaseManager.Email,
                    Attorney = caseDetails.Attorney.Name,
                    AttorneyPhone = caseDetails.Attorney.Phone,
                    AttorneyEmail = caseDetails.Attorney.Email,
                    StateOfIncident = caseDetails.StateOfIncident,
                    MedicalStatus = caseDetails.MedicalStatus,
                    TrackingFollowUpDate = caseDetails.TrackingFollowUpDate,
                    PlaintiffDob = caseEntity.ClientDob,
                    PlaintiffPhone = caseEntity.ClientPhone ?? string.Empty,
                    PlaintiffEmail = caseEntity.ClientEmail ?? string.Empty,
                    PlaintiffAddress = address.Address,
                    PlaintiffCity = address.City,
                    PlaintiffState = address.State,
                    PlaintiffZip = address.Zip,
                    CaseEnteredBy = caseDetails.CaseEnteredBy,
                    CaseDropped = caseDetails.CaseDropped,
                    MinorComp = caseDetails.MinorComp,
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
        IReadOnlyCollection<Lien> rowLiens,
        IReadOnlyDictionary<Guid, Case> casesById,
        IReadOnlyCollection<ServicingItem> servicingItems,
        bool includeTrackingNotes,
        bool includeCaseActivity,
        bool includeFeedNotes,
        CancellationToken ct)
    {
        var lienIds = liens.Select(lien => lien.Id).Distinct().ToList();
        var rowLienIds = rowLiens.Select(lien => lien.Id).ToHashSet();
        var facilityInfoByLienId = servicingItems
            .Where(item => item.LienId.HasValue &&
                           rowLienIds.Contains(item.LienId.Value) &&
                           string.Equals(item.TaskType, "LegacyMedicalFacilityInfo", StringComparison.Ordinal))
            .GroupBy(item => item.LienId!.Value)
            .ToDictionary(group => group.Key, group => group.OrderByDescending(item => item.CreatedAtUtc).First());

        var caseMetadataById = casesById.Values
            .ToDictionary(caseEntity => caseEntity.Id, caseEntity => ParseLegacyNoteFields(caseEntity.Notes));

        var facilityIds = new HashSet<Guid>(rowLiens
            .Where(lien => lien.FacilityId.HasValue)
            .Select(lien => lien.FacilityId!.Value));
        var contactIds = new HashSet<Guid>();
        var companyIds = new HashSet<Guid>();
        var companyContactPersonIds = new HashSet<Guid>();

        foreach (var lien in rowLiens)
        {
            AddGuidIfValid(companyIds, lien.MedicalProviderCompanyId?.ToString());
            AddGuidIfValid(companyIds, lien.MedicalFacilityCompanyId?.ToString());
        }

        foreach (var caseEntity in casesById.Values)
        {
            var fields = caseMetadataById[caseEntity.Id];
            AddGuidIfValid(contactIds, fields.GetValueOrDefault("lawFirmId"));
            AddGuidIfValid(contactIds, fields.GetValueOrDefault("caseManagerId"));
            AddGuidIfValid(contactIds, fields.GetValueOrDefault("attorneyId"));
            AddGuidIfValid(contactIds, fields.GetValueOrDefault("attorney"));
            AddGuidIfValid(companyIds, caseEntity.HandlingLawFirmCompanyId?.ToString());
            AddGuidIfValid(companyContactPersonIds, caseEntity.CaseManagerContactPersonId?.ToString());
            AddGuidIfValid(companyContactPersonIds, caseEntity.AttorneyContactPersonId?.ToString());
        }

        foreach (var lien in rowLiens)
        {
            if (facilityInfoByLienId.TryGetValue(lien.Id, out var facilityInfo))
            {
                var fields = ParseLegacyNoteFields(facilityInfo.Notes);
                AddGuidIfValid(facilityIds, fields.GetValueOrDefault("facilityId"));
                AddGuidIfValid(contactIds, fields.GetValueOrDefault("medicalProviderId"));
                AddGuidIfValid(contactIds, fields.GetValueOrDefault("facilityContactPersonId"));
                AddGuidIfValid(contactIds, fields.GetValueOrDefault("medicalFacilityContactId"));
            }
        }

        contactIds.UnionWith(facilityIds);

        var facilitiesById = (await _facilityRepo.GetByIdsAsync(tenantId, facilityIds, ct))
            .ToDictionary(facility => facility.Id);
        var contactsById = (await _contactRepo.GetByIdsAsync(tenantId, contactIds, ct))
            .ToDictionary(contact => contact.Id);
        var companiesById = (await _companyRepo.GetCompaniesByIdsAsync(tenantId, companyIds, ct))
            .ToDictionary(company => company.Id);
        var companyContactPersonsById = (await _companyRepo.GetContactPersonsByIdsAsync(
                tenantId,
                companyContactPersonIds,
                ct))
            .ToDictionary(contact => contact.Id);
        var medicalCodeFieldsByLienId = servicingItems
            .Where(item => item.LienId.HasValue &&
                           rowLienIds.Contains(item.LienId.Value) &&
                           string.Equals(item.TaskType, "LegacyMedicalCode", StringComparison.Ordinal))
            .GroupBy(item => item.LienId!.Value)
            .ToDictionary(
                group => group.Key,
                group => group.Select(item => ParseLegacyNoteFields(item.Notes)).ToList());
        var medicalDetailsByLienId = new Dictionary<Guid, MedicalLienReportDetails>();
        foreach (var lien in rowLiens)
        {
            var facilityFields = facilityInfoByLienId.TryGetValue(lien.Id, out var facilityInfo)
                ? ParseLegacyNoteFields(facilityInfo.Notes)
                : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var facilityId = FirstGuid(
                facilityFields.GetValueOrDefault("facilityId"),
                lien.FacilityId?.ToString());
            facilitiesById.TryGetValue(facilityId ?? Guid.Empty, out var facility);
            companiesById.TryGetValue(lien.MedicalFacilityCompanyId ?? Guid.Empty, out var facilityCompany);
            var facilityContactId = FirstGuid(
                facilityFields.GetValueOrDefault("facilityContactPersonId"),
                facilityFields.GetValueOrDefault("medicalFacilityContactId"));
            contactsById.TryGetValue(facilityContactId ?? Guid.Empty, out var facilityContact);

            var providerId = FirstGuid(facilityFields.GetValueOrDefault("medicalProviderId"));
            contactsById.TryGetValue(providerId ?? Guid.Empty, out var providerContact);
            companiesById.TryGetValue(lien.MedicalProviderCompanyId ?? Guid.Empty, out var providerCompany);

            var codes = medicalCodeFieldsByLienId
                .GetValueOrDefault(lien.Id, [])
                .Select(fields => FirstNonEmpty(
                    fields.GetValueOrDefault("code"),
                    fields.GetValueOrDefault("description")))
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.OrdinalIgnoreCase);

            medicalDetailsByLienId[lien.Id] = new MedicalLienReportDetails(
                FirstNonEmpty(facilityCompany?.Name, facility?.Name, facilityFields.GetValueOrDefault("facilityName")) ?? string.Empty,
                facilityContact is null ? string.Empty : DisplayPersonName(facilityContact),
                FirstNonEmpty(facilityCompany?.AddressLine1, facility?.AddressLine1) ?? string.Empty,
                FirstNonEmpty(facilityCompany?.City, facility?.City) ?? string.Empty,
                FirstNonEmpty(facilityCompany?.State, facility?.State) ?? string.Empty,
                FirstNonEmpty(facilityCompany?.PostalCode, facility?.PostalCode) ?? string.Empty,
                FirstNonEmpty(
                    providerCompany?.Name,
                    providerContact is null ? null : DisplayContactName(providerContact),
                    facilityFields.GetValueOrDefault("medicalProvider")) ?? string.Empty,
                string.Join(", ", codes));
        }

        var caseDetailsById = new Dictionary<Guid, CaseReportDetails>();
        foreach (var caseEntity in casesById.Values)
        {
            var fields = caseMetadataById[caseEntity.Id];
            var lawFirmId = FirstGuid(fields.GetValueOrDefault("lawFirmId"));
            var caseManagerId = FirstGuid(fields.GetValueOrDefault("caseManagerId"));
            var attorneyId = FirstGuid(fields.GetValueOrDefault("attorneyId"), fields.GetValueOrDefault("attorney"));
            contactsById.TryGetValue(lawFirmId ?? Guid.Empty, out var legacyLawFirm);
            contactsById.TryGetValue(caseManagerId ?? Guid.Empty, out var legacyCaseManager);
            contactsById.TryGetValue(attorneyId ?? Guid.Empty, out var legacyAttorney);
            companiesById.TryGetValue(caseEntity.HandlingLawFirmCompanyId ?? Guid.Empty, out var canonicalLawFirm);
            companyContactPersonsById.TryGetValue(caseEntity.CaseManagerContactPersonId ?? Guid.Empty, out var canonicalCaseManager);
            companyContactPersonsById.TryGetValue(caseEntity.AttorneyContactPersonId ?? Guid.Empty, out var canonicalAttorney);

            caseDetailsById[caseEntity.Id] = new CaseReportDetails(
                BuildPartyDetails(canonicalLawFirm, legacyLawFirm, fields.GetValueOrDefault("lawFirm")),
                BuildPersonDetails(canonicalCaseManager, legacyCaseManager, fields.GetValueOrDefault("caseManager")),
                BuildPersonDetails(canonicalAttorney, legacyAttorney, fields.GetValueOrDefault("attorneyName")),
                FirstNonEmpty(
                    fields.GetValueOrDefault("accidentType"),
                    fields.GetValueOrDefault("caseType")) ?? string.Empty,
                NormalizeYesNoFlag(
                    fields.GetValueOrDefault("isUccFiled"),
                    fields.GetValueOrDefault("isUCCFiled")),
                FirstNonEmpty(
                    caseEntity.IncidentState,
                    fields.GetValueOrDefault("stateOfIncident"),
                    fields.GetValueOrDefault("accidentState")) ?? string.Empty,
                FirstNonEmpty(caseEntity.CurrentMedicalStatus, fields.GetValueOrDefault("currentMedicalStatus")) ?? string.Empty,
                caseEntity.TrackingFollowUpDate ?? ParseLegacyDate(fields.GetValueOrDefault("trackingFollowUpDate")),
                FirstNonEmpty(
                    caseEntity.ImportedCreatedByName,
                    fields.GetValueOrDefault("caseEnteredBy"),
                    fields.GetValueOrDefault("createdBy")) ?? string.Empty,
                FormatNullableFlag(caseEntity.CaseDropped, fields.GetValueOrDefault("caseDropped")),
                FormatNullableFlag(caseEntity.MinorComp, fields.GetValueOrDefault("minorComp")));
        }

        var reductions = await _reductionRepo.GetByLienIdsAsync(tenantId, rowLienIds, ct);
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
            medicalDetailsByLienId,
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
                   string.Equals(key, "last_case_tracking_date", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(key, "last_activity", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(key, "last_activity_date", StringComparison.OrdinalIgnoreCase);
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

    private static string DisplayPersonName(CompanyContactPerson contact)
        => string.Join(" ", new[] { contact.FirstName, contact.LastName }
            .Where(value => !string.IsNullOrWhiteSpace(value)));

    private static PartyReportDetails BuildPartyDetails(
        Company? company,
        Contact? contact,
        string? fallbackName) => new(
        FirstNonEmpty(company?.Name, contact is null ? null : DisplayContactName(contact), fallbackName) ?? string.Empty,
        FirstNonEmpty(company?.AddressLine1, contact?.AddressLine1) ?? string.Empty,
        FirstNonEmpty(company?.City, contact?.City) ?? string.Empty,
        FirstNonEmpty(company?.State, contact?.State) ?? string.Empty,
        FirstNonEmpty(company?.PostalCode, contact?.PostalCode) ?? string.Empty,
        FirstNonEmpty(company?.Phone, contact?.Phone) ?? string.Empty,
        FirstNonEmpty(company?.Email, contact?.Email) ?? string.Empty);

    private static PersonReportDetails BuildPersonDetails(
        CompanyContactPerson? canonical,
        Contact? legacy,
        string? fallbackName) => new(
        FirstNonEmpty(
            canonical is null ? null : DisplayPersonName(canonical),
            legacy is null ? null : DisplayPersonName(legacy),
            fallbackName) ?? string.Empty,
        FirstNonEmpty(canonical?.Phone, legacy?.Phone) ?? string.Empty,
        FirstNonEmpty(canonical?.Email, legacy?.Email) ?? string.Empty);

    private static AddressParts ResolveClientAddress(Case? caseEntity)
    {
        if (caseEntity is null)
            return new AddressParts();

        var fallback = SplitAddress(caseEntity.ClientAddress);
        return new AddressParts(
            FirstNonEmpty(caseEntity.ClientAddressLine1, fallback.Address) ?? string.Empty,
            FirstNonEmpty(caseEntity.ClientCity, fallback.City) ?? string.Empty,
            FirstNonEmpty(caseEntity.ClientState, fallback.State) ?? string.Empty,
            FirstNonEmpty(caseEntity.ClientPostalCode, fallback.Zip) ?? string.Empty);
    }

    private static AddressParts SplitAddress(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return new AddressParts();

        var parts = value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts.Length switch
        {
            >= 4 => new AddressParts(string.Join(", ", parts.Take(parts.Length - 3)), parts[^3], parts[^2], parts[^1]),
            3 => new AddressParts(parts[0], parts[1], parts[2], string.Empty),
            2 => new AddressParts(parts[0], parts[1], string.Empty, string.Empty),
            _ => new AddressParts(value.Trim(), string.Empty, string.Empty, string.Empty),
        };
    }

    private static DateOnly? ParseLegacyDate(string? value)
        => TryParseLegacyDate(value, out var date) ? date : null;

    private static string FormatNullableFlag(bool? typedValue, string? compatibilityValue)
    {
        if (typedValue.HasValue)
            return typedValue.Value ? "Yes" : "No";

        if (string.IsNullOrWhiteSpace(compatibilityValue))
            return string.Empty;

        return compatibilityValue.Trim().ToUpperInvariant() switch
        {
            "TRUE" or "YES" or "Y" or "1" => "Yes",
            "FALSE" or "NO" or "N" or "0" => "No",
            _ => compatibilityValue.Trim(),
        };
    }

    private static SettlementReportAmounts BuildSettlementReportAmounts(
        IGrouping<Guid, LienSettlement> settlements)
    {
        var reductionAmount = 0m;
        var returnedAmount = 0m;
        var hasLegacyReductionAmount = false;
        var hasLegacyReturnedAmount = false;
        DateOnly? reductionDate = null;

        foreach (var settlement in settlements)
        {
            var fields = ParseLegacyNoteFields(settlement.Note);
            if (fields.TryGetValue("reductionAmount", out var reductionValue))
            {
                hasLegacyReductionAmount = true;
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
            reductionDate);
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
        if (enrichment.ReductionAmountsByLienId.TryGetValue(lien.Id, out var reductionAmounts))
            return reductionAmounts.Amount;

        if (enrichment.SettlementAmountsByLienId.TryGetValue(lien.Id, out var settlementAmounts) &&
            settlementAmounts.HasLegacyReductionAmount)
        {
            return settlementAmounts.ReductionAmount;
        }

        return 0m;
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
        if (enrichment.ReductionAmountsByLienId.TryGetValue(lien.Id, out var reductionAmounts))
            return reductionAmounts.ReductionDate;

        if (enrichment.SettlementAmountsByLienId.TryGetValue(lien.Id, out var settlementAmounts) &&
            settlementAmounts.ReductionDate.HasValue)
        {
            return settlementAmounts.ReductionDate;
        }

        return null;
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
            ? DateOnly.FromDateTime(DateTime.UtcNow).DayNumber - purchaseDate.Value.DayNumber
            : null;

    private static int? GetDaysSinceReductionApproval(DateOnly? reductionDate, DateOnly? paidDate) =>
        reductionDate.HasValue && paidDate.HasValue
            ? Math.Max(paidDate.Value.DayNumber - reductionDate.Value.DayNumber, 0)
            : null;

    private sealed record PartyReportDetails(
        string Name,
        string Address,
        string City,
        string State,
        string Zip,
        string Phone,
        string Email)
    {
        public static readonly PartyReportDetails Empty = new(
            string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty);
    }

    private sealed record PersonReportDetails(string Name, string Phone, string Email)
    {
        public static readonly PersonReportDetails Empty = new(string.Empty, string.Empty, string.Empty);
    }

    private sealed record CaseReportDetails(
        PartyReportDetails LawFirm,
        PersonReportDetails CaseManager,
        PersonReportDetails Attorney,
        string CaseType,
        string UccFiled,
        string StateOfIncident,
        string MedicalStatus,
        DateOnly? TrackingFollowUpDate,
        string CaseEnteredBy,
        string CaseDropped,
        string MinorComp)
    {
        public static readonly CaseReportDetails Empty = new(
            PartyReportDetails.Empty,
            PersonReportDetails.Empty,
            PersonReportDetails.Empty,
            string.Empty,
            "No",
            string.Empty,
            string.Empty,
            null,
            string.Empty,
            string.Empty,
            string.Empty);
    }

    private sealed record MedicalLienReportDetails(
        string FacilityName,
        string FacilityContact,
        string FacilityAddress,
        string FacilityCity,
        string FacilityState,
        string FacilityZip,
        string Provider,
        string Codes)
    {
        public static readonly MedicalLienReportDetails Empty = new(
            string.Empty, string.Empty, string.Empty, string.Empty,
            string.Empty, string.Empty, string.Empty, string.Empty);
    }

    private sealed record AddressParts(
        string Address = "",
        string City = "",
        string State = "",
        string Zip = "");

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
        IReadOnlyDictionary<Guid, MedicalLienReportDetails> MedicalDetailsByLienId,
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
