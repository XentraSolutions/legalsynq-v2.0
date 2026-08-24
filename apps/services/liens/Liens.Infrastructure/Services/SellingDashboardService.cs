using BuildingBlocks.Exceptions;
using Liens.Application.DTOs;
using Liens.Application.Interfaces;
using Liens.Domain.Entities;
using Liens.Domain.Enums;
using Liens.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Liens.Infrastructure.Services;

public sealed class SellingDashboardService : ISellingDashboardService
{
    private const int MaxPage = 100_000;
    private const int MaxPageSize = 100;
    private static readonly HashSet<string> Tabs = new(StringComparer.OrdinalIgnoreCase)
    {
        "pending",
        "internal",
        "sold",
        "archived",
        "all",
    };

    private static readonly HashSet<string> SortFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "lienid",
        "fundingcompany",
        "initialservicedate",
        "billingamount",
        "askamount",
        "highestbid",
        "status",
    };

    private readonly LiensDbContext _db;

    public SellingDashboardService(LiensDbContext db)
    {
        _db = db;
    }

    public async Task<SellingDashboardResponse> GetAsync(
        Guid tenantId,
        Guid sellerOrgId,
        SellingDashboardQuery query,
        CancellationToken ct = default)
    {
        var normalizedQuery = Normalize(query);

        var includeArchived = string.Equals(normalizedQuery.Tab, "archived", StringComparison.Ordinal);
        var lienQuery = _db.Liens
            .AsNoTracking()
            .Where(l => l.TenantId == tenantId
                && (l.SellingOrgId == sellerOrgId
                    || (!l.SellingOrgId.HasValue && l.OrgId == sellerOrgId)));

        lienQuery = includeArchived
            ? lienQuery.Where(l => l.ArchivedAtUtc != null || l.SellerStatus == SellingLienStatus.Archived)
            : lienQuery.Where(l => l.ArchivedAtUtc == null && l.SellerStatus != SellingLienStatus.Archived);

        if (normalizedQuery.FundingCompanyId.HasValue)
            lienQuery = lienQuery.Where(l =>
                l.FundingCompanyCompanyId == normalizedQuery.FundingCompanyId
                || (!l.FundingCompanyCompanyId.HasValue
                    && l.FundingCompanyId == normalizedQuery.FundingCompanyId));
        if (normalizedQuery.FacilityId.HasValue)
            lienQuery = lienQuery.Where(l => l.FacilityId == normalizedQuery.FacilityId);
        if (normalizedQuery.InitialServiceDateFrom.HasValue)
            lienQuery = lienQuery.Where(l => l.InitialServiceDate.HasValue &&
                l.InitialServiceDate.Value >= normalizedQuery.InitialServiceDateFrom.Value);
        if (normalizedQuery.InitialServiceDateTo.HasValue)
            lienQuery = lienQuery.Where(l => l.InitialServiceDate.HasValue &&
                l.InitialServiceDate.Value <= normalizedQuery.InitialServiceDateTo.Value);

        // Keep the seller dashboard read model deliberately narrow. Materializing the
        // full Lien entity makes this read path depend on every newly mapped column,
        // including compatibility columns that the dashboard does not use. A partial
        // production migration would then take both dashboard and list endpoints down.
        var liens = await lienQuery
            .Select(l => new DashboardLien(
                l.Id,
                l.LienNumber,
                l.ExternalReference,
                l.SubjectFirstName,
                l.SubjectLastName,
                l.CaseId,
                l.FacilityId,
                l.FundingCompanyId,
                l.FundingCompanyCompanyId,
                l.InitialServiceDate,
                l.OriginalAmount,
                l.AskAmount,
                l.HighestBidAmount,
                l.PurchasePrice,
                l.SellerStatus,
                l.Status))
            .ToListAsync(ct);

        var context = await LoadContextAsync(tenantId, sellerOrgId, liens, ct);
        var rows = liens
            .Select(lien => CreateRow(lien, context))
            .Where(row => MatchesFilters(row, normalizedQuery))
            .ToList();

        var summary = BuildSummary(rows);
        var tabRows = rows
            .Where(row => MatchesTab(row.Status, normalizedQuery.Tab))
            .ToList();

        IReadOnlyDictionary<Guid, decimal> highestBids;
        List<DashboardRow> sorted;
        if (string.Equals(normalizedQuery.SortBy, "highestbid", StringComparison.Ordinal))
        {
            highestBids = await LoadHighestBidsAsync(tenantId, sellerOrgId, tabRows, ct);
            sorted = Sort(tabRows, normalizedQuery.SortBy, normalizedQuery.SortDirection, highestBids);
        }
        else
        {
            highestBids = new Dictionary<Guid, decimal>();
            sorted = Sort(tabRows, normalizedQuery.SortBy, normalizedQuery.SortDirection, highestBids);
        }

        var pageRows = sorted
            .Skip((normalizedQuery.Page - 1) * normalizedQuery.PageSize)
            .Take(normalizedQuery.PageSize)
            .ToList();
        if (!string.Equals(normalizedQuery.SortBy, "highestbid", StringComparison.Ordinal))
            highestBids = await LoadHighestBidsAsync(tenantId, sellerOrgId, pageRows, ct);

        var items = pageRows.Select(row => Map(row, highestBids)).ToList();

        return new SellingDashboardResponse
        {
            Summary = summary,
            Items = items,
            Page = normalizedQuery.Page,
            PageSize = normalizedQuery.PageSize,
            TotalCount = tabRows.Count,
        };
    }

    private async Task<DashboardContext> LoadContextAsync(
        Guid tenantId,
        Guid sellerOrgId,
        IReadOnlyCollection<DashboardLien> liens,
        CancellationToken ct)
    {
        var caseIds = liens
            .Where(l => l.CaseId.HasValue)
            .Select(l => l.CaseId!.Value)
            .Distinct()
            .ToList();
        var facilityIds = liens
            .Where(l => l.FacilityId.HasValue)
            .Select(l => l.FacilityId!.Value)
            .Distinct()
            .ToList();

        var cases = caseIds.Count == 0
            ? []
            : await _db.Cases.AsNoTracking()
                .Where(c => c.TenantId == tenantId && caseIds.Contains(c.Id))
                .Select(c => new DashboardCase(
                    c.Id,
                    c.OrgId,
                    c.CaseNumber,
                    c.Notes,
                    c.HandlingLawFirmCompanyId,
                    c.CaseManagerContactPersonId))
                .ToListAsync(ct);
        var facilities = facilityIds.Count == 0
            ? []
            : await _db.Facilities.AsNoTracking()
                .Where(f => f.TenantId == tenantId && facilityIds.Contains(f.Id))
                .Select(f => new DashboardFacility(f.Id, f.Name))
                .ToListAsync(ct);

        var caseById = cases.ToDictionary(c => c.Id);
        var lawFirmOrgIds = cases.Select(c => c.OrgId).Distinct().ToHashSet();
        var referencedContactIds = liens
            .Where(l => !l.FundingCompanyCompanyId.HasValue && l.FundingCompanyId.HasValue)
            .Select(l => l.FundingCompanyId!.Value)
            .ToHashSet();
        var referencedCompanyIds = liens
            .Where(l => l.FundingCompanyCompanyId.HasValue)
            .Select(l => l.FundingCompanyCompanyId!.Value)
            .ToHashSet();
        var referencedCompanyContactIds = cases
            .Where(c => c.CaseManagerContactPersonId.HasValue)
            .Select(c => c.CaseManagerContactPersonId!.Value)
            .ToHashSet();

        foreach (var caseEntity in cases)
        {
            if (caseEntity.HandlingLawFirmCompanyId.HasValue)
                referencedCompanyIds.Add(caseEntity.HandlingLawFirmCompanyId.Value);

            var fields = ParseLegacyNoteFields(caseEntity.Notes);
            if (!caseEntity.HandlingLawFirmCompanyId.HasValue)
                AddGuid(fields, "lawFirmId", referencedContactIds);
            if (!caseEntity.CaseManagerContactPersonId.HasValue)
                AddGuid(fields, "caseManagerId", referencedContactIds);
        }

        var contacts = (referencedContactIds.Count == 0 && lawFirmOrgIds.Count == 0)
            ? []
            : await _db.Contacts.AsNoTracking()
                .Where(c => c.TenantId == tenantId &&
                    (referencedContactIds.Contains(c.Id) || referencedContactIds.Contains(c.OrgId) || lawFirmOrgIds.Contains(c.OrgId)))
                .ToListAsync(ct);
        var companies = referencedCompanyIds.Count == 0
            ? []
            : await _db.Companies.AsNoTracking()
                .Where(c => c.TenantId == tenantId
                    && c.OrgId == sellerOrgId
                    && referencedCompanyIds.Contains(c.Id))
                .ToListAsync(ct);
        var companyContacts = referencedCompanyContactIds.Count == 0
            ? []
            : await _db.CompanyContactPersons.AsNoTracking()
                .Where(c => c.TenantId == tenantId
                    && referencedCompanyContactIds.Contains(c.Id)
                    && referencedCompanyIds.Contains(c.CompanyId))
                .ToListAsync(ct);

        return new DashboardContext(
            caseById,
            facilities.ToDictionary(f => f.Id),
            contacts.GroupBy(c => c.Id).ToDictionary(g => g.Key, g => g.First()),
            contacts.GroupBy(c => c.OrgId).ToDictionary(g => g.Key, g => g.First()),
            companies.ToDictionary(c => c.Id),
            companyContacts.ToDictionary(c => c.Id));
    }

    private async Task<IReadOnlyDictionary<Guid, decimal>> LoadHighestBidsAsync(
        Guid tenantId,
        Guid sellerOrgId,
        IReadOnlyCollection<DashboardRow> rows,
        CancellationToken ct)
    {
        var lienIds = rows.Select(row => row.Lien.Id).ToList();
        if (lienIds.Count == 0)
            return new Dictionary<Guid, decimal>();

        var nowUtc = DateTime.UtcNow;
        var offerBids = await _db.LienOffers
            .AsNoTracking()
            .Where(offer =>
                offer.TenantId == tenantId &&
                offer.SellerOrgId == sellerOrgId &&
                lienIds.Contains(offer.LienId) &&
                offer.Status != OfferStatus.Rejected &&
                offer.Status != OfferStatus.Withdrawn &&
                offer.Status != OfferStatus.Expired &&
                !(offer.Status == OfferStatus.Pending &&
                  offer.ExpiresAtUtc.HasValue &&
                  offer.ExpiresAtUtc.Value <= nowUtc))
            .GroupBy(offer => offer.LienId)
            .Select(group => new
            {
                LienId = group.Key,
                Amount = group.Max(offer => offer.OfferAmount),
            })
            .ToDictionaryAsync(item => item.LienId, item => item.Amount, ct);

        return rows.ToDictionary(
            row => row.Lien.Id,
            row => offerBids.GetValueOrDefault(row.Lien.Id, row.Lien.HighestBidAmount ?? 0m));
    }

    private static DashboardRow CreateRow(DashboardLien lien, DashboardContext context)
    {
        context.CasesById.TryGetValue(lien.CaseId ?? Guid.Empty, out var caseEntity);
        context.FacilitiesById.TryGetValue(lien.FacilityId ?? Guid.Empty, out var facility);

        var caseFields = caseEntity is null
            ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            : ParseLegacyNoteFields(caseEntity.Notes);
        var lawFirmId = caseEntity?.HandlingLawFirmCompanyId
            ?? TryGetGuid(caseFields, "lawFirmId")
            ?? caseEntity?.OrgId;
        var caseManagerId = caseEntity?.CaseManagerContactPersonId
            ?? TryGetGuid(caseFields, "caseManagerId");
        var fundingCompanyId = lien.FundingCompanyCompanyId ?? lien.FundingCompanyId;

        var fundingCompany = lien.FundingCompanyCompanyId.HasValue
            ? context.CompaniesById.GetValueOrDefault(lien.FundingCompanyCompanyId.Value)?.Name
            : ResolveContactLabel(lien.FundingCompanyId, context.ContactsById, context.ContactsByOrgId);
        var lawFirm = caseEntity?.HandlingLawFirmCompanyId.HasValue == true
            ? context.CompaniesById.GetValueOrDefault(caseEntity.HandlingLawFirmCompanyId.Value)?.Name
            : ResolveContactLabel(lawFirmId, context.ContactsById, context.ContactsByOrgId);
        var caseManager = caseEntity?.CaseManagerContactPersonId.HasValue == true
            ? ResolveCompanyContactLabel(
                caseEntity.CaseManagerContactPersonId,
                context.CompanyContactsById)
            : ResolveContactLabel(caseManagerId, context.ContactsById, context.ContactsByOrgId);

        return new DashboardRow(
            lien,
            caseEntity?.CaseNumber,
            fundingCompanyId,
            lawFirmId,
            lawFirm,
            caseManagerId,
            caseManager,
            fundingCompany,
            facility?.Name,
            StatusFor(lien));
    }

    private static bool MatchesFilters(DashboardRow row, SellingDashboardQuery query)
    {
        if (query.FundingCompanyId.HasValue && row.FundingCompanyId != query.FundingCompanyId)
            return false;
        if (query.LawFirmId.HasValue && row.LawFirmId != query.LawFirmId)
            return false;
        if (query.CaseManagerId.HasValue && row.CaseManagerId != query.CaseManagerId)
            return false;
        if (query.FacilityId.HasValue && row.Lien.FacilityId != query.FacilityId)
            return false;
        if (query.InitialServiceDateFrom.HasValue &&
            (!row.Lien.InitialServiceDate.HasValue || row.Lien.InitialServiceDate.Value < query.InitialServiceDateFrom.Value))
            return false;
        if (query.InitialServiceDateTo.HasValue &&
            (!row.Lien.InitialServiceDate.HasValue || row.Lien.InitialServiceDate.Value > query.InitialServiceDateTo.Value))
            return false;

        return string.IsNullOrWhiteSpace(query.Search) || MatchesSearch(row, query.Search);
    }

    private static bool MatchesSearch(DashboardRow row, string search)
    {
        var term = search.Trim();
        return new[]
        {
            row.Lien.LienNumber,
            row.Lien.ExternalReference,
            row.Lien.SubjectFirstName,
            row.Lien.SubjectLastName,
            row.CaseNumber,
            row.FundingCompany,
            row.LawFirm,
            row.CaseManager,
            row.Facility,
        }.Any(value => value?.Contains(term, StringComparison.OrdinalIgnoreCase) == true);
    }

    private static SellingDashboardSummary BuildSummary(IReadOnlyCollection<DashboardRow> rows)
    {
        var totalPending = 0m;
        var totalInternal = 0m;
        var totalSold = 0m;
        var pendingCount = 0;
        var internalCount = 0;
        var soldCount = 0;

        foreach (var row in rows)
        {
            switch (row.Status)
            {
                case var status when IsPendingTabStatus(status):
                    totalPending += row.Lien.OriginalAmount;
                    pendingCount++;
                    break;
                case SellingLienStatus.Internal:
                    totalInternal += row.Lien.OriginalAmount;
                    internalCount++;
                    break;
                case SellingLienStatus.Sold:
                    totalSold += row.Lien.OriginalAmount;
                    soldCount++;
                    break;
            }
        }

        return new SellingDashboardSummary
        {
            TotalPortfolioValue = totalPending + totalInternal + totalSold,
            TotalPending = totalPending,
            TotalInternal = totalInternal,
            TotalSold = totalSold,
            PendingCount = pendingCount,
            InternalCount = internalCount,
            SoldCount = soldCount,
        };
    }

    private static List<DashboardRow> Sort(
        IReadOnlyCollection<DashboardRow> rows,
        string sortBy,
        string sortDirection,
        IReadOnlyDictionary<Guid, decimal> highestBids)
    {
        var descending = string.Equals(sortDirection, "desc", StringComparison.OrdinalIgnoreCase);
        IOrderedEnumerable<DashboardRow> ordered = sortBy switch
        {
            "lienid" => descending
                ? rows.OrderByDescending(row => row.Lien.LienNumber, StringComparer.OrdinalIgnoreCase)
                : rows.OrderBy(row => row.Lien.LienNumber, StringComparer.OrdinalIgnoreCase),
            "fundingcompany" => descending
                ? rows.OrderByDescending(row => row.FundingCompany ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                : rows.OrderBy(row => row.FundingCompany ?? string.Empty, StringComparer.OrdinalIgnoreCase),
            "billingamount" => descending
                ? rows.OrderByDescending(row => row.Lien.OriginalAmount)
                : rows.OrderBy(row => row.Lien.OriginalAmount),
            "askamount" => descending
                ? rows.OrderByDescending(row => row.Lien.AskAmount ?? decimal.MinValue)
                : rows.OrderBy(row => row.Lien.AskAmount ?? decimal.MinValue),
            "highestbid" => descending
                ? rows.OrderByDescending(row => highestBids.GetValueOrDefault(row.Lien.Id))
                : rows.OrderBy(row => highestBids.GetValueOrDefault(row.Lien.Id)),
            "status" => descending
                ? rows.OrderByDescending(row => row.Status, StringComparer.OrdinalIgnoreCase)
                : rows.OrderBy(row => row.Status, StringComparer.OrdinalIgnoreCase),
            _ => descending
                ? rows.OrderByDescending(row => row.Lien.InitialServiceDate ?? DateOnly.MinValue)
                : rows.OrderBy(row => row.Lien.InitialServiceDate ?? DateOnly.MinValue),
        };

        return ordered
            .ThenBy(row => row.Lien.LienNumber, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static SellingDashboardLienItem Map(
        DashboardRow row,
        IReadOnlyDictionary<Guid, decimal> highestBids) => new()
    {
        LienId = row.Lien.Id,
        LienNumber = row.Lien.LienNumber,
        CaseId = row.Lien.CaseId,
        CaseNumber = row.CaseNumber,
        FundingCompanyId = row.FundingCompanyId,
        FundingCompany = row.FundingCompany,
        LawFirmId = row.LawFirmId,
        LawFirm = row.LawFirm,
        CaseManagerId = row.CaseManagerId,
        CaseManager = row.CaseManager,
        FacilityId = row.Lien.FacilityId,
        Facility = row.Facility,
        InitialServiceDate = row.Lien.InitialServiceDate,
        BillingAmount = row.Lien.OriginalAmount,
        AskAmount = row.Lien.AskAmount,
        HighestBidAmount = highestBids.GetValueOrDefault(row.Lien.Id),
        PurchasePrice = row.Lien.PurchasePrice,
        Status = row.Status,
    };

    private static bool MatchesTab(string status, string tab) => tab switch
    {
        "pending" => IsPendingTabStatus(status),
        "internal" => status == SellingLienStatus.Internal,
        "sold" => status == SellingLienStatus.Sold,
        "archived" => status == SellingLienStatus.Archived,
        _ => true,
    };

    private static bool IsPendingTabStatus(string status) =>
        status is SellingLienStatus.Pending
            or SellingLienStatus.Approval
            or SellingLienStatus.PreparedForSale
            or SellingLienStatus.SubmittedForSale;

    private static SellingDashboardQuery Normalize(SellingDashboardQuery query)
    {
        var tab = string.IsNullOrWhiteSpace(query.Tab) ? "pending" : query.Tab.Trim().ToLowerInvariant();
        var sortBy = string.IsNullOrWhiteSpace(query.SortBy)
            ? "initialservicedate"
            : query.SortBy.Trim().Replace("_", string.Empty, StringComparison.Ordinal).ToLowerInvariant();
        var sortDirection = string.IsNullOrWhiteSpace(query.SortDirection)
            ? "desc"
            : query.SortDirection.Trim().ToLowerInvariant();

        var errors = new Dictionary<string, string[]>();
        if (!Tabs.Contains(tab))
            errors["tab"] = ["Tab must be pending, internal, sold, archived, or all."];
        if (!SortFields.Contains(sortBy))
            errors["sortBy"] = ["SortBy must be lienId, fundingCompany, initialServiceDate, billingAmount, askAmount, highestBid, or status."];
        if (sortDirection is not "asc" and not "desc")
            errors["sortDirection"] = ["SortDirection must be asc or desc."];
        if (query.Page < 1)
            errors["page"] = ["Page must be at least 1."];
        if (query.Page > MaxPage)
            errors["page"] = [$"Page must not exceed {MaxPage}."];
        if (query.PageSize is < 1 or > MaxPageSize)
            errors["pageSize"] = [$"PageSize must be between 1 and {MaxPageSize}."];
        if (query.InitialServiceDateFrom.HasValue && query.InitialServiceDateTo.HasValue &&
            query.InitialServiceDateFrom.Value > query.InitialServiceDateTo.Value)
            errors["initialServiceDateTo"] = ["Initial service end date must not be before the start date."];

        if (errors.Count > 0)
            throw new ValidationException("Selling dashboard query is invalid.", errors);

        return new SellingDashboardQuery
        {
            Tab = tab,
            Search = query.Search?.Trim(),
            FundingCompanyId = query.FundingCompanyId,
            LawFirmId = query.LawFirmId,
            CaseManagerId = query.CaseManagerId,
            FacilityId = query.FacilityId,
            InitialServiceDateFrom = query.InitialServiceDateFrom,
            InitialServiceDateTo = query.InitialServiceDateTo,
            SortBy = sortBy,
            SortDirection = sortDirection,
            Page = query.Page,
            PageSize = query.PageSize,
        };
    }

    private static string StatusFor(DashboardLien lien)
    {
        if (!string.IsNullOrWhiteSpace(lien.SellerStatus))
        {
            return string.Equals(lien.SellerStatus, SellingLienStatus.Accepted, StringComparison.Ordinal)
                ? SellingLienStatus.Sold
                : lien.SellerStatus;
        }

        return lien.Status switch
        {
            LienStatus.Sold => SellingLienStatus.Sold,
            LienStatus.Declined => SellingLienStatus.Declined,
            LienStatus.Withdrawn => SellingLienStatus.Withdrawn,
            LienStatus.Accepted => SellingLienStatus.Sold,
            LienStatus.Offered or LienStatus.UnderReview => SellingLienStatus.SubmittedForSale,
            _ => SellingLienStatus.Pending,
        };
    }

    private static string? ResolveContactLabel(
        Guid? id,
        IReadOnlyDictionary<Guid, Contact> contactsById,
        IReadOnlyDictionary<Guid, Contact> contactsByOrgId)
    {
        if (!id.HasValue)
            return null;

        if (!contactsById.TryGetValue(id.Value, out var contact) &&
            !contactsByOrgId.TryGetValue(id.Value, out contact))
            return null;

        return string.IsNullOrWhiteSpace(contact.Organization)
            ? contact.DisplayName
            : contact.Organization;
    }

    private static string? ResolveCompanyContactLabel(
        Guid? id,
        IReadOnlyDictionary<Guid, CompanyContactPerson> contactsById)
    {
        if (!id.HasValue || !contactsById.TryGetValue(id.Value, out var contact))
            return null;

        return string.Join(" ", new[] { contact.FirstName, contact.LastName }
            .Where(value => !string.IsNullOrWhiteSpace(value)));
    }

    private static Dictionary<string, string> ParseLegacyNoteFields(string? notes)
    {
        var fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(notes))
            return fields;

        foreach (var part in notes.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var separator = part.IndexOf('=');
            if (separator <= 0)
                continue;

            var key = part[..separator].Trim();
            var value = part[(separator + 1)..].Trim();
            if (!string.IsNullOrWhiteSpace(key) && !string.IsNullOrWhiteSpace(value))
                fields[key] = value;
        }

        return fields;
    }

    private static Guid? TryGetGuid(IReadOnlyDictionary<string, string> fields, string key)
        => fields.TryGetValue(key, out var value) && Guid.TryParse(value, out var id) ? id : null;

    private static void AddGuid(IReadOnlyDictionary<string, string> fields, string key, ISet<Guid> target)
    {
        if (TryGetGuid(fields, key) is { } id)
            target.Add(id);
    }

    private sealed record DashboardContext(
        IReadOnlyDictionary<Guid, DashboardCase> CasesById,
        IReadOnlyDictionary<Guid, DashboardFacility> FacilitiesById,
        IReadOnlyDictionary<Guid, Contact> ContactsById,
        IReadOnlyDictionary<Guid, Contact> ContactsByOrgId,
        IReadOnlyDictionary<Guid, Company> CompaniesById,
        IReadOnlyDictionary<Guid, CompanyContactPerson> CompanyContactsById);

    private sealed record DashboardLien(
        Guid Id,
        string LienNumber,
        string? ExternalReference,
        string? SubjectFirstName,
        string? SubjectLastName,
        Guid? CaseId,
        Guid? FacilityId,
        Guid? FundingCompanyId,
        Guid? FundingCompanyCompanyId,
        DateOnly? InitialServiceDate,
        decimal OriginalAmount,
        decimal? AskAmount,
        decimal? HighestBidAmount,
        decimal? PurchasePrice,
        string? SellerStatus,
        string Status);

    private sealed record DashboardCase(
        Guid Id,
        Guid OrgId,
        string CaseNumber,
        string? Notes,
        Guid? HandlingLawFirmCompanyId,
        Guid? CaseManagerContactPersonId);

    private sealed record DashboardFacility(Guid Id, string Name);

    private sealed record DashboardRow(
        DashboardLien Lien,
        string? CaseNumber,
        Guid? FundingCompanyId,
        Guid? LawFirmId,
        string? LawFirm,
        Guid? CaseManagerId,
        string? CaseManager,
        string? FundingCompany,
        string? Facility,
        string Status);
}
