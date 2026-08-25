using Liens.Application.Repositories;
using Liens.Domain.Entities;
using Liens.Domain.Enums;
using Liens.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Liens.Infrastructure.Repositories;

public class CaseRepository : ICaseRepository
{
    private readonly LiensDbContext _db;

    public CaseRepository(LiensDbContext db)
    {
        _db = db;
    }

    public async Task<Case?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default)
    {
        return await _db.Cases
            .Where(c => c.TenantId == tenantId && c.Id == id)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<List<Case>> GetByIdsAsync(Guid tenantId, IReadOnlyCollection<Guid> ids, CancellationToken ct = default)
    {
        if (ids.Count == 0)
            return [];

        return await _db.Cases
            .Where(c => c.TenantId == tenantId && ids.Contains(c.Id))
            .ToListAsync(ct);
    }

    public async Task<Case?> GetByCaseNumberAsync(Guid tenantId, string caseNumber, CancellationToken ct = default)
    {
        return await _db.Cases
            .Where(c => c.TenantId == tenantId && c.CaseNumber == caseNumber)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<Case?> GetByExternalReferenceAsync(Guid tenantId, string externalReference, CancellationToken ct = default)
    {
        return await _db.Cases
            .Where(c => c.TenantId == tenantId && c.ExternalReference == externalReference)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<List<Case>> GetByCaseNumberPrefixAsync(Guid tenantId, string caseNumberPrefix, CancellationToken ct = default)
    {
        return await _db.Cases
            .Where(c => c.TenantId == tenantId && c.CaseNumber.StartsWith(caseNumberPrefix))
            .ToListAsync(ct);
    }

    public async Task<List<Case>> GetPotentialDuplicateCandidatesAsync(
        Guid tenantId,
        DateOnly clientDob,
        DateOnly dateOfIncident,
        CancellationToken ct = default)
    {
        return await _db.Cases
            .AsNoTracking()
            .Where(c =>
                c.TenantId == tenantId &&
                c.ClientDob == clientDob &&
                c.DateOfIncident == dateOfIncident)
            .OrderByDescending(c => c.CreatedAtUtc)
            .Take(25)
            .ToListAsync(ct);
    }

    public async Task<List<Case>> SearchUnlinkedReportCasesAsync(
        Guid tenantId,
        string? search,
        IReadOnlyCollection<string> statuses,
        IReadOnlyCollection<Guid> caseIds,
        IReadOnlyCollection<Guid> lawFirmIds,
        IReadOnlyCollection<Guid> attorneyIds,
        IReadOnlyCollection<Guid> caseManagerIds,
        CancellationToken ct = default)
    {
        var query = _db.Cases.AsNoTracking().Where(caseEntity =>
            caseEntity.TenantId == tenantId &&
            !_db.Liens.Any(lien =>
                lien.TenantId == tenantId &&
                lien.CaseId == caseEntity.Id));

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(caseEntity =>
                caseEntity.CaseNumber.Contains(term) ||
                caseEntity.ClientFirstName.Contains(term) ||
                caseEntity.ClientLastName.Contains(term) ||
                (caseEntity.Title != null && caseEntity.Title.Contains(term)) ||
                (caseEntity.ExternalReference != null && caseEntity.ExternalReference.Contains(term)));
        }

        if (statuses.Count > 0)
            query = ApplyCaseStatusFilter(query, string.Join(',', statuses));

        if (caseIds.Count > 0)
        {
            var requestedCaseIds = caseIds.ToList();
            query = query.Where(caseEntity => requestedCaseIds.Contains(caseEntity.Id));
        }

        if (lawFirmIds.Count > 0)
        {
            var requestedLawFirmIds = lawFirmIds.ToList();
            var directMatches = query.Where(caseEntity =>
                requestedLawFirmIds.Contains(caseEntity.OrgId) ||
                (caseEntity.HandlingLawFirmCompanyId.HasValue &&
                 requestedLawFirmIds.Contains(caseEntity.HandlingLawFirmCompanyId.Value)));
            var legacyMatches = ApplyMetadataFilter(query, "lawFirmId", string.Join(',', requestedLawFirmIds));
            query = directMatches.Concat(legacyMatches).Distinct();
        }

        if (attorneyIds.Count > 0)
            query = ApplyMetadataFilter(query, "attorneyId", string.Join(',', attorneyIds));

        if (caseManagerIds.Count > 0)
        {
            var requestedCaseManagerIds = caseManagerIds.ToList();
            var directMatches = query.Where(caseEntity =>
                caseEntity.CaseManagerContactPersonId.HasValue &&
                requestedCaseManagerIds.Contains(caseEntity.CaseManagerContactPersonId.Value));
            var legacyMatches = ApplyMetadataFilter(query, "caseManagerId", string.Join(',', requestedCaseManagerIds));
            query = directMatches.Concat(legacyMatches).Distinct();
        }

        return await query
            .OrderByDescending(caseEntity => caseEntity.CreatedAtUtc)
            .ThenByDescending(caseEntity => caseEntity.Id)
            .ToListAsync(ct);
    }

    public async Task<(List<Case> Items, int TotalCount)> SearchAsync(
        Guid tenantId, string? search, string? status,
        int page, int pageSize,
        Guid? orgId = null,
        string? sortBy = null,
        string? sortDirection = null,
        string? accidentTypeId = null,
        string? caseManagerId = null,
        string? lawFirmIds = null,
        CancellationToken ct = default)
    {
        var q = _db.Cases.Where(c => c.TenantId == tenantId);

        if (orgId.HasValue)
            q = q.Where(c => c.OrgId == orgId.Value);

        q = ApplyLawFirmFilter(q, lawFirmIds);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            var tokens = term.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            if (tokens.Length > 1)
            {
                foreach (var token in tokens)
                {
                    var local = token;
                    q = q.Where(c =>
                        c.ClientFirstName.Contains(local) ||
                        c.ClientLastName.Contains(local));
                }
            }
            else
            {
                q = q.Where(c =>
                    c.CaseNumber.Contains(term) ||
                    c.ClientFirstName.Contains(term) ||
                    c.ClientLastName.Contains(term) ||
                    (c.Title != null && c.Title.Contains(term)) ||
                    (c.ExternalReference != null && c.ExternalReference.Contains(term)));
            }
        }

        q = ApplyCaseStatusFilter(q, status);

        q = ApplyMetadataFilter(q, "accidentTypeId", accidentTypeId);
        q = ApplyMetadataFilter(q, "caseManagerId", caseManagerId);

        var totalCount = await q.CountAsync(ct);

        var descending = string.Equals(sortDirection, "DESC", StringComparison.OrdinalIgnoreCase) ||
                         !string.Equals(sortDirection, "ASC", StringComparison.OrdinalIgnoreCase);

        q = (sortBy ?? string.Empty).ToLowerInvariant() switch
        {
            "caseid" => descending ? q.OrderByDescending(c => c.Id) : q.OrderBy(c => c.Id),
            "casecode" => descending ? q.OrderByDescending(c => c.CaseNumber) : q.OrderBy(c => c.CaseNumber),
            "fullname" => descending
                ? q.OrderByDescending(c => c.ClientLastName).ThenByDescending(c => c.ClientFirstName)
                : q.OrderBy(c => c.ClientLastName).ThenBy(c => c.ClientFirstName),
            "dateofloss" => descending ? q.OrderByDescending(c => c.DateOfIncident) : q.OrderBy(c => c.DateOfIncident),
            "dateofbirth" => descending ? q.OrderByDescending(c => c.ClientDob) : q.OrderBy(c => c.ClientDob),
            "status" => descending ? q.OrderByDescending(c => c.Status) : q.OrderBy(c => c.Status),
            _ => q.OrderByDescending(c => c.CreatedAtUtc),
        };

        var items = await q
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return (items, totalCount);
    }

    private static IQueryable<Case> ApplyLawFirmFilter(IQueryable<Case> query, string? rawValues)
    {
        var values = SplitFilterValues(rawValues);
        if (values.Count == 0)
            return query;

        var orgIds = values
            .Where(value => Guid.TryParse(value, out _))
            .Select(Guid.Parse)
            .ToList();

        IQueryable<Case> matches = orgIds.Count == 0
            ? query.Where(_ => false)
            : query.Where(caseEntity => orgIds.Contains(caseEntity.OrgId));

        foreach (var value in values)
            matches = matches.Concat(FilterByMetadataToken(query, "lawFirmId", value));

        return matches.Distinct();
    }

    private static IQueryable<Case> ApplyCaseStatusFilter(
        IQueryable<Case> query,
        string? rawValues)
    {
        var values = SplitFilterValues(rawValues);
        if (values.Count == 0)
            return query;

        IQueryable<Case>? matches = null;
        foreach (var value in values)
        {
            var valueMatches = FilterByCaseStatus(query, value);
            matches = matches is null ? valueMatches : matches.Concat(valueMatches);
        }

        return matches!.Distinct();
    }

    private static IQueryable<Case> FilterByCaseStatus(
        IQueryable<Case> query,
        string value)
    {
        var normalized = value.Trim()
            .Replace("-", string.Empty, StringComparison.Ordinal)
            .Replace(" ", string.Empty, StringComparison.Ordinal)
            .Replace("(", string.Empty, StringComparison.Ordinal)
            .Replace(")", string.Empty, StringComparison.Ordinal)
            .ToUpperInvariant();

        return normalized switch
        {
            "N" or "NEW" => FilterByLegacyStatusLabel(
                query, CaseStatus.PreDemand, ["New"], ["N", "New"]),
            "P" or "PROCESSING" => FilterByLegacyStatusLabel(
                query, CaseStatus.PreDemand, ["Processing"], ["P", "Processing"]),
            "PD" or "PREDEMAND" => FilterByCanonicalStatus(
                query, CaseStatus.PreDemand, ["Pre-Demand", "PreDemand"], ["PD", "Pre-Demand"]),
            "DS" or "DEMANDSENT" => FilterByCanonicalStatus(
                query, CaseStatus.DemandSent, ["Demand Sent", "DemandSent"], ["DS", "Demand Sent"]),
            "NT" or "NEGOTIATIONS" or "INNEGOTIATION" => FilterByCanonicalStatus(
                query,
                CaseStatus.InNegotiation,
                ["Negotiations", "In Negotiation", "InNegotiation"],
                ["NT", "Negotiations", "In Negotiation"]),
            "LITIGATION" => FilterByLegacyStatusLabel(
                query, CaseStatus.InNegotiation, ["Litigation"], ["Litigation"]),
            "LP" or "LITIGATIONPENDING" => FilterByLitigationStatus(
                query,
                CaseStatus.LitigationPending,
                ["Litigation (Pending)", "Litigation(Pending)"],
                ["LP", "Litigation (Pending)", "Litigation(Pending)"]),
            "LO" or "LITIGATIONOPEN" => FilterByLitigationStatus(
                query,
                CaseStatus.LitigationOpen,
                ["Litigation (Open)", "Litigation(Open)"],
                ["LO", "Litigation (Open)", "Litigation(Open)"]),
            "LC" or "LITIGATIONCLOSE" or "LITIGATIONCLOSED" => FilterByLegacyStatusLabel(
                query,
                CaseStatus.InNegotiation,
                ["Litigation (Closed)", "Litigation(Closed)"],
                ["LC", "Litigation (Closed)", "Litigation(Closed)"]),
            "CS" or "CASESETTLED" => FilterByCanonicalStatus(
                query, CaseStatus.CaseSettled, ["Case Settled", "CaseSettled"], ["CS", "Case Settled"]),
            "C" or "CLOSED" => FilterByCanonicalStatus(
                query, CaseStatus.Closed, ["Closed"], ["C"]),
            _ => query.Where(caseEntity => caseEntity.Status == value.Trim()),
        };
    }

    private static IQueryable<Case> FilterByLitigationStatus(
        IQueryable<Case> query,
        string canonicalStatus,
        IReadOnlyCollection<string> labels,
        IReadOnlyCollection<string> historicalStatuses) =>
        FilterByCanonicalStatus(query, canonicalStatus, labels, historicalStatuses);

    private static IQueryable<Case> FilterByCanonicalStatus(
        IQueryable<Case> query,
        string canonicalStatus,
        IReadOnlyCollection<string> acceptedLabels,
        IReadOnlyCollection<string> historicalStatuses)
    {
        var canonicalQuery = query.Where(caseEntity => caseEntity.Status == canonicalStatus);
        IQueryable<Case> matches = canonicalQuery.Where(caseEntity =>
            caseEntity.Notes == null || !caseEntity.Notes.Contains("statusLabel="));

        foreach (var label in acceptedLabels)
            matches = matches.Concat(FilterByMetadataToken(canonicalQuery, "statusLabel", label));

        foreach (var historicalStatus in historicalStatuses)
            matches = matches.Concat(query.Where(caseEntity => caseEntity.Status == historicalStatus));

        return matches.Distinct();
    }

    private static IQueryable<Case> FilterByLegacyStatusLabel(
        IQueryable<Case> query,
        string canonicalStatus,
        IReadOnlyCollection<string> labels,
        IReadOnlyCollection<string> historicalStatuses)
    {
        var canonicalQuery = query.Where(caseEntity => caseEntity.Status == canonicalStatus);
        IQueryable<Case> matches = query.Where(_ => false);

        foreach (var label in labels)
            matches = matches.Concat(FilterByMetadataToken(canonicalQuery, "statusLabel", label));

        foreach (var historicalStatus in historicalStatuses)
            matches = matches.Concat(query.Where(caseEntity => caseEntity.Status == historicalStatus));

        return matches.Distinct();
    }

    private static IQueryable<Case> ApplyMetadataFilter(
        IQueryable<Case> query,
        string key,
        string? rawValues)
    {
        var values = SplitFilterValues(rawValues);
        if (values.Count == 0)
            return query;

        IQueryable<Case>? matches = null;
        foreach (var value in values)
        {
            var valueMatches = FilterByMetadataToken(query, key, value);
            matches = matches is null ? valueMatches : matches.Concat(valueMatches);
        }

        return matches!.Distinct();
    }

    private static IQueryable<Case> FilterByMetadataToken(
        IQueryable<Case> query,
        string key,
        string value)
    {
        var token = $"{key}={value}";
        return query.Where(caseEntity => caseEntity.Notes != null &&
            (caseEntity.Notes == token ||
             caseEntity.Notes.StartsWith(token + ";") ||
             caseEntity.Notes.Contains(";" + token + ";") ||
             caseEntity.Notes.Contains("; " + token + ";") ||
             caseEntity.Notes.Contains(Environment.NewLine + token + ";") ||
             caseEntity.Notes.EndsWith(";" + token) ||
             caseEntity.Notes.EndsWith("; " + token) ||
             caseEntity.Notes.EndsWith(Environment.NewLine + token)));
    }

    private static List<string> SplitFilterValues(string? rawValues) =>
        string.IsNullOrWhiteSpace(rawValues)
            ? []
            : rawValues.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

    public async Task AddAsync(Case entity, CancellationToken ct = default)
    {
        await _db.Cases.AddAsync(entity, ct);
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Case entity, CancellationToken ct = default)
    {
        _db.Cases.Update(entity);
        await _db.SaveChangesAsync(ct);
    }
}
