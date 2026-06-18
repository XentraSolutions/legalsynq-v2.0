using Liens.Application.Repositories;
using Liens.Domain.Entities;
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

    public async Task<Case?> GetByCaseNumberAsync(Guid tenantId, string caseNumber, CancellationToken ct = default)
    {
        return await _db.Cases
            .Where(c => c.TenantId == tenantId && c.CaseNumber == caseNumber)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<List<Case>> GetByCaseNumberPrefixAsync(Guid tenantId, string caseNumberPrefix, CancellationToken ct = default)
    {
        return await _db.Cases
            .Where(c => c.TenantId == tenantId && c.CaseNumber.StartsWith(caseNumberPrefix))
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
        CancellationToken ct = default)
    {
        var q = _db.Cases.Where(c => c.TenantId == tenantId);

        if (orgId.HasValue)
            q = q.Where(c => c.OrgId == orgId.Value);

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

        if (!string.IsNullOrWhiteSpace(status))
        {
            var statuses = status
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToList();

            if (statuses.Count == 1)
            {
                var single = statuses[0];
                q = q.Where(c => c.Status == single);
            }
            else if (statuses.Count > 1)
            {
                q = q.Where(c => statuses.Contains(c.Status));
            }
        }

        if (!string.IsNullOrWhiteSpace(accidentTypeId))
        {
            var accidentTypeToken = $"accidentTypeId={accidentTypeId.Trim()}";
            q = q.Where(c => c.Notes != null &&
                (c.Notes == accidentTypeToken ||
                 c.Notes.StartsWith(accidentTypeToken + ";") ||
                 c.Notes.Contains("; " + accidentTypeToken + ";") ||
                 c.Notes.EndsWith("; " + accidentTypeToken)));
        }

        if (!string.IsNullOrWhiteSpace(caseManagerId))
        {
            var caseManagerToken = $"caseManagerId={caseManagerId.Trim()}";
            q = q.Where(c => c.Notes != null &&
                (c.Notes == caseManagerToken ||
                 c.Notes.StartsWith(caseManagerToken + ";") ||
                 c.Notes.Contains("; " + caseManagerToken + ";") ||
                 c.Notes.EndsWith("; " + caseManagerToken)));
        }

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
