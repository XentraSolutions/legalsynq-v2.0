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
    private readonly ILienReductionRepository   _reductionRepo;
    private readonly ISettlementPaymentDetailRepository _paymentDetailRepo;

    public DIYReportService(
        IDIYReportConfigRepository repo,
        ILienRepository lienRepo,
        ICaseRepository caseRepo,
        IServicingItemRepository servicingItemRepo,
        IContactRepository contactRepo,
        IFacilityRepository facilityRepo,
        ILienReductionRepository reductionRepo,
        ISettlementPaymentDetailRepository paymentDetailRepo)
    {
        _repo              = repo;
        _lienRepo          = lienRepo;
        _caseRepo          = caseRepo;
        _servicingItemRepo = servicingItemRepo;
        _contactRepo       = contactRepo;
        _facilityRepo      = facilityRepo;
        _reductionRepo     = reductionRepo;
        _paymentDetailRepo = paymentDetailRepo;
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
        Guid tenantId, DIYReportRunRequest request, CancellationToken ct = default)
    {
        // Extract known filters from the config payload
        string? search = null;
        var lienStatuses = new List<string>();
        var caseStatuses = new List<string>();
        var reportType = GetReportString(request, "reportType")?.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(reportType))
            reportType = "LIENS";
        var isLiensReport = string.Equals(reportType, "LIENS", StringComparison.OrdinalIgnoreCase);
        var isCombinedReport = string.Equals(reportType, "COMBINED", StringComparison.OrdinalIgnoreCase);
        var isCasesReport = !isLiensReport && !isCombinedReport;

        if (HasReportProperty(request, "status", out var s) && s.ValueKind == JsonValueKind.String)
        {
            var status = s.GetString();
            if (!string.IsNullOrWhiteSpace(status))
                lienStatuses.Add(status.Trim());
        }
        if (HasReportProperty(request, "statusView", out var statusView) && statusView.ValueKind == JsonValueKind.String)
        {
            var status = statusView.GetString();
            if (!string.IsNullOrWhiteSpace(status))
            {
                ApplyStatusView(status.Trim(), isLiensReport || isCombinedReport, lienStatuses, caseStatuses);
            }
        }
        if (HasReportProperty(request, "lienStatusIds", out var statusIds) && statusIds.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in statusIds.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(item.GetString()))
                    lienStatuses.Add(item.GetString()!.Trim());
            }
        }
        if (HasReportProperty(request, "search", out var k) && k.ValueKind == JsonValueKind.String)
            search = k.GetString();
        var purchaseDateFrom = GetReportDateOnly(request, "purchaseDateFrom");
        var purchaseDateTo = GetReportDateOnly(request, "purchaseDateTo");
        var closedDateFrom = GetReportDateTime(request, "closedDateFrom", endOfDay: false);
        var closedDateTo = GetReportDateTime(request, "closedDateTo", endOfDay: true);
        var isBulk = GetReportString(request, "isBulk");
        var filterCaseIds = GetReportGuidList(request, "plaintiffCaseIds");

        var page = GetReportInt(request, "page", request.Page);
        if (page < 1) page = 1;
        var limit = GetReportInt(request, "limit", request.Limit);
        if (limit < 1) limit = 50;
        if (limit > 500) limit = 500;

        var reportData = await _lienRepo.SearchReportAsync(
            tenantId,
            search,
            lienStatuses.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            caseStatuses.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
            purchaseDateFrom,
            purchaseDateTo,
            closedDateFrom,
            closedDateTo,
            NormalizeLegacyBooleanFilter(isBulk),
            filterCaseIds,
            page,
            limit,
            ct);

        var rowSource = isCasesReport ? reportData.AllItems : reportData.PageItems;
        var caseIds = rowSource
            .Select(l => l.CaseId)
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Distinct()
            .ToList();
        var casesById = new Dictionary<Guid, Case>();
        foreach (var caseId in caseIds)
        {
            var caseEntity = await _caseRepo.GetByIdAsync(tenantId, caseId, ct);
            if (caseEntity is not null)
                casesById[caseId] = caseEntity;
        }

        var legacyMedicalAmounts = await GetLegacyMedicalAmountsByLienIdAsync(
            tenantId,
            reportData.AllItems,
            ct);
        var rowEnrichment = await GetRowEnrichmentAsync(
            tenantId,
            reportData.AllItems,
            casesById,
            ct);

        var rows = isCasesReport
            ? BuildCaseRows(reportData.AllItems, casesById, legacyMedicalAmounts, rowEnrichment, page, limit)
            : reportData.PageItems.Select(l => BuildLienRow(l, casesById, legacyMedicalAmounts, rowEnrichment)).ToList();

        var totalCount = isCasesReport
            ? reportData.AllItems.Select(l => l.CaseId).Where(id => id.HasValue).Distinct().Count()
            : reportData.TotalCount;

        var summary = await BuildSummaryAsync(
            tenantId,
            reportData.AllItems,
            legacyMedicalAmounts,
            rowEnrichment.ReturnedAmountsByLienId,
            ct);

        return new DIYReportResult
        {
            ReportType = reportType,
            Items      = rows,
            TotalCount = totalCount,
            Page       = page,
            PageSize   = limit,
            SummaryTotals = summary,
        };
    }

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
                    lienStatuses.AddRange(LienStatus.Terminal);
                else
                    caseStatuses.AddRange(CaseStatus.All.Where(IsClosedCaseStatus));
                return;
            case "REJECTED":
                if (isLienLevel)
                    lienStatuses.Add(LienStatus.Cancelled);
                return;
            default:
                if (isLienLevel && LienStatus.All.Contains(statusView))
                    lienStatuses.Add(statusView);
                else if (CaseStatus.All.Contains(statusView))
                    caseStatuses.Add(statusView);
                return;
        }
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
            DateClosed = l.ClosedAtUtc ?? caseEntity?.ClosedAtUtc,
            InitialServiceDate = l.InitialServiceDate,
            BillingAmount = ResolveBillingAmount(l, legacyMedicalAmounts),
            PurchaseAmount = ResolvePurchaseAmount(l, legacyMedicalAmounts),
            ReturnedAmount = ResolveReturnedAmount(l, enrichment.ReturnedAmountsByLienId),
            LienTotal = l.CurrentBalance,
            NumberOfLiens = 1,
            ToSettleAmount = l.CurrentBalance,
            SettledAmount = l.PayoffAmount,
            DaysSinceReductionApproval = enrichment.LatestReductionDateByLienId.TryGetValue(l.Id, out var reductionDate)
                ? GetDaysSinceReductionApproval(reductionDate)
                : 0,
            MedicalFacility = enrichment.MedicalFacilityByLienId.GetValueOrDefault(l.Id, string.Empty),
            LawFirm = caseDetails.LawFirm,
            CaseType = caseDetails.CaseType,
            CaseManager = caseDetails.CaseManager,
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
        return liens
            .Where(l => l.CaseId.HasValue)
            .GroupBy(l => l.CaseId!.Value)
            .OrderByDescending(g => g.Key)
            .Skip((page - 1) * limit)
            .Take(limit)
            .Select(g =>
            {
                casesById.TryGetValue(g.Key, out var caseEntity);
                var first = g.First();
                var caseDetails = enrichment.CaseDetailsById.GetValueOrDefault(g.Key, CaseReportDetails.Empty);
                var purchaseDates = g
                    .Where(l => l.PurchaseDate.HasValue)
                    .Select(l => l.PurchaseDate!.Value)
                    .ToList();
                var reductionDates = g
                    .Select(l => enrichment.LatestReductionDateByLienId.TryGetValue(l.Id, out var date)
                        ? (DateOnly?)date
                        : null)
                    .Where(date => date.HasValue)
                    .Select(date => date!.Value)
                    .ToList();
                var facilities = g
                    .Select(l => enrichment.MedicalFacilityByLienId.GetValueOrDefault(l.Id, string.Empty))
                    .Where(name => !string.IsNullOrWhiteSpace(name))
                    .Distinct(StringComparer.OrdinalIgnoreCase);

                return new DIYReportRow
                {
                    CaseId = g.Key,
                    LienId = null,
                    CaseNumber = caseEntity?.CaseNumber ?? string.Empty,
                    LienNumber = string.Empty,
                    PlaintiffFirstName = caseEntity?.ClientFirstName ?? first.SubjectFirstName ?? string.Empty,
                    PlaintiffLastName = caseEntity?.ClientLastName ?? first.SubjectLastName ?? string.Empty,
                    ClientName = caseEntity is null
                        ? string.Join(" ", new[] { first.SubjectFirstName, first.SubjectLastName }.Where(v => !string.IsNullOrWhiteSpace(v)))
                        : $"{caseEntity.ClientFirstName} {caseEntity.ClientLastName}".Trim(),
                    Status = null,
                    CaseStatus = caseEntity?.Status,
                    DateOfLoss = caseEntity?.DateOfIncident ?? first.IncidentDate,
                    PurchaseDate = purchaseDates.Count > 0
                        ? purchaseDates.Min()
                        : null,
                    DateClosed = caseEntity?.ClosedAtUtc ?? g.Max(l => l.ClosedAtUtc),
                    InitialServiceDate = g.Min(l => l.InitialServiceDate),
                    BillingAmount = g.Sum(l => ResolveBillingAmount(l, legacyMedicalAmounts)),
                    PurchaseAmount = g.Sum(l => ResolvePurchaseAmount(l, legacyMedicalAmounts) ?? 0m),
                    ReturnedAmount = g.Sum(l => ResolveReturnedAmount(l, enrichment.ReturnedAmountsByLienId) ?? 0m),
                    LienTotal = g.Sum(l => l.CurrentBalance ?? 0m),
                    NumberOfLiens = g.Count(),
                    ToSettleAmount = g.Sum(l => l.CurrentBalance ?? 0m),
                    SettledAmount = g.Sum(l => l.PayoffAmount ?? 0m),
                    DaysSinceReductionApproval = reductionDates.Count > 0
                        ? GetDaysSinceReductionApproval(reductionDates.Max())
                        : 0,
                    MedicalFacility = string.Join(", ", facilities),
                    LawFirm = caseDetails.LawFirm,
                    CaseType = caseDetails.CaseType,
                    CaseManager = caseDetails.CaseManager,
                    Extra = new Dictionary<string, object?>(),
                };
            })
            .ToList();
    }

    private async Task<ReportRowEnrichment> GetRowEnrichmentAsync(
        Guid tenantId,
        IReadOnlyCollection<Lien> liens,
        IReadOnlyDictionary<Guid, Case> casesById,
        CancellationToken ct)
    {
        var lienIds = liens.Select(lien => lien.Id).Distinct().ToList();
        var servicingItems = await _servicingItemRepo.GetByLienIdsAsync(tenantId, lienIds, ct);
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
                    fields.GetValueOrDefault("caseType")) ?? string.Empty);
        }

        var reductions = await _reductionRepo.GetByLienIdsAsync(tenantId, lienIds, ct);
        var latestReductionDateByLienId = reductions
            .GroupBy(reduction => reduction.LienId)
            .ToDictionary(group => group.Key, group => group.Max(reduction => reduction.ReductionDate));

        var returnedAmountsByLienId = (await _paymentDetailRepo.GetByLienIdsAsync(tenantId, lienIds, ct))
            .GroupBy(payment => payment.LienId)
            .ToDictionary(group => group.Key, group => group.Sum(payment => payment.Amount));

        return new ReportRowEnrichment(
            medicalFacilityByLienId,
            caseDetailsById,
            latestReductionDateByLienId,
            returnedAmountsByLienId);
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

    private static string DisplayContactName(Contact contact)
        => FirstNonEmpty(contact.Organization, contact.DisplayName) ?? string.Empty;

    private static string DisplayPersonName(Contact contact)
        => FirstNonEmpty(contact.DisplayName, contact.Organization) ?? string.Empty;

    private static decimal? ResolveReturnedAmount(
        Lien lien,
        IReadOnlyDictionary<Guid, decimal> returnedAmountsByLienId)
        => lien.PayoffAmount ??
           (returnedAmountsByLienId.TryGetValue(lien.Id, out var returnedAmount) ? returnedAmount : null);

    private static int GetDaysSinceReductionApproval(DateOnly reductionDate) =>
        Math.Max(DateOnly.FromDateTime(DateTime.UtcNow).DayNumber - reductionDate.DayNumber, 0);

    private sealed record CaseReportDetails(string LawFirm, string CaseManager, string CaseType)
    {
        public static readonly CaseReportDetails Empty = new(string.Empty, string.Empty, string.Empty);
    }

    private sealed record ReportRowEnrichment(
        IReadOnlyDictionary<Guid, string> MedicalFacilityByLienId,
        IReadOnlyDictionary<Guid, CaseReportDetails> CaseDetailsById,
        IReadOnlyDictionary<Guid, DateOnly> LatestReductionDateByLienId,
        IReadOnlyDictionary<Guid, decimal> ReturnedAmountsByLienId);

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

    private static string? NormalizeLegacyBooleanFilter(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        return value.Trim().ToUpperInvariant() switch
        {
            "Y" or "YES" or "TRUE" or "1" => "Yes",
            "N" or "NO" or "FALSE" or "0" => "No",
            _ => value.Trim(),
        };
    }

    private async Task<DIYReportSummaryTotals> BuildSummaryAsync(
        Guid tenantId,
        IReadOnlyCollection<Lien> liens,
        IReadOnlyDictionary<Guid, LegacyMedicalAmounts> legacyMedicalAmounts,
        IReadOnlyDictionary<Guid, decimal> returnedAmountsByLienId,
        CancellationToken ct)
    {
        var totalPurchase = liens.Sum(l => ResolvePurchaseAmount(l, legacyMedicalAmounts) ?? 0m);
        var totalBilling = liens.Sum(l => ResolveBillingAmount(l, legacyMedicalAmounts));
        var totalReturned = liens.Sum(l => ResolveReturnedAmount(l, returnedAmountsByLienId) ?? 0m);
        var totalAmtToSettle = liens.Sum(l => l.CurrentBalance ?? 0m);
        var totalGrossProfit = totalReturned - totalPurchase;
        var avgRoi = totalPurchase == 0m ? 0m : totalReturned / totalPurchase;
        var closedLiens = liens.Count(l => LienStatus.Terminal.Contains(l.Status));
        var uniqueCaseIds = liens
            .Select(l => l.CaseId)
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Distinct()
            .ToList();
        var closedCases = 0;

        foreach (var caseId in uniqueCaseIds)
        {
            var caseEntity = await _caseRepo.GetByIdAsync(tenantId, caseId, ct);
            if (caseEntity is not null &&
                IsClosedCaseStatus(caseEntity.Status))
            {
                closedCases++;
            }
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
            TotalOpenLiens = liens.Count - closedLiens,
            TotalClosedLiens = closedLiens,
        };
    }

    private async Task<IReadOnlyDictionary<Guid, LegacyMedicalAmounts>> GetLegacyMedicalAmountsByLienIdAsync(
        Guid tenantId,
        IReadOnlyCollection<Lien> liens,
        CancellationToken ct)
    {
        var lienIds = liens.Select(l => l.Id).Distinct().ToList();
        var medicalCodeItems = await _servicingItemRepo.GetByLienIdsAsync(tenantId, lienIds, ct);

        return medicalCodeItems
            .Where(item => item.LienId.HasValue &&
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
            : lien.PurchasePrice;

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
