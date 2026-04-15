using Microsoft.EntityFrameworkCore;
using Reports.Contracts.Persistence;
using Reports.Domain.Entities;

namespace Reports.Infrastructure.Persistence;

public sealed class EfTemplateRepository : ITemplateRepository
{
    private readonly ReportsDbContext _db;

    public EfTemplateRepository(ReportsDbContext db) => _db = db;

    public async Task<ReportDefinition?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        return await _db.ReportDefinitions
            .Include(d => d.Versions.Where(v => v.IsActive))
            .FirstOrDefaultAsync(d => d.Id == id, ct);
    }

    public async Task<ReportDefinition?> GetByCodeAsync(string code, CancellationToken ct)
    {
        return await _db.ReportDefinitions
            .Include(d => d.Versions.Where(v => v.IsActive))
            .FirstOrDefaultAsync(d => d.Code == code, ct);
    }

    public async Task<IReadOnlyList<ReportDefinition>> ListAsync(string? productCode, bool? activeOnly, int page, int pageSize, CancellationToken ct)
    {
        var query = _db.ReportDefinitions.AsQueryable();

        if (!string.IsNullOrEmpty(productCode))
            query = query.Where(d => d.ProductCode == productCode);

        if (activeOnly == true)
            query = query.Where(d => d.IsActive);

        return await query
            .OrderBy(d => d.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);
    }

    public async Task<ReportDefinition> CreateAsync(ReportDefinition definition, CancellationToken ct)
    {
        if (definition.Id == Guid.Empty)
            definition.Id = Guid.NewGuid();

        _db.ReportDefinitions.Add(definition);
        await _db.SaveChangesAsync(ct);
        return definition;
    }

    public async Task<ReportDefinition> UpdateAsync(ReportDefinition definition, CancellationToken ct)
    {
        _db.ReportDefinitions.Update(definition);
        await _db.SaveChangesAsync(ct);
        return definition;
    }

    public async Task<ReportTemplateVersion?> GetVersionAsync(Guid definitionId, int versionNumber, CancellationToken ct)
    {
        return await _db.ReportTemplateVersions
            .FirstOrDefaultAsync(v => v.ReportDefinitionId == definitionId && v.VersionNumber == versionNumber, ct);
    }

    public async Task<ReportTemplateVersion?> GetActiveVersionAsync(Guid definitionId, CancellationToken ct)
    {
        return await _db.ReportTemplateVersions
            .Where(v => v.ReportDefinitionId == definitionId && v.IsActive)
            .OrderByDescending(v => v.VersionNumber)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyList<ReportTemplateVersion>> ListVersionsAsync(Guid definitionId, CancellationToken ct)
    {
        return await _db.ReportTemplateVersions
            .Where(v => v.ReportDefinitionId == definitionId)
            .OrderByDescending(v => v.VersionNumber)
            .ToListAsync(ct);
    }

    public async Task<ReportTemplateVersion> CreateVersionAsync(ReportTemplateVersion version, CancellationToken ct)
    {
        if (version.Id == Guid.Empty)
            version.Id = Guid.NewGuid();

        _db.ReportTemplateVersions.Add(version);
        await _db.SaveChangesAsync(ct);
        return version;
    }
}
