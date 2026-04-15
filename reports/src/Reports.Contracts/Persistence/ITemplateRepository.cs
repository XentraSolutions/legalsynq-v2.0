using Reports.Domain.Entities;

namespace Reports.Contracts.Persistence;

public interface ITemplateRepository
{
    Task<ReportDefinition?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<ReportDefinition?> GetByCodeAsync(string code, CancellationToken ct = default);
    Task<IReadOnlyList<ReportDefinition>> ListAsync(string? productCode = null, bool? activeOnly = null, int page = 1, int pageSize = 20, CancellationToken ct = default);
    Task<ReportDefinition> CreateAsync(ReportDefinition definition, CancellationToken ct = default);
    Task<ReportDefinition> UpdateAsync(ReportDefinition definition, CancellationToken ct = default);
    Task<ReportTemplateVersion?> GetVersionAsync(Guid definitionId, int versionNumber, CancellationToken ct = default);
    Task<ReportTemplateVersion?> GetActiveVersionAsync(Guid definitionId, CancellationToken ct = default);
    Task<IReadOnlyList<ReportTemplateVersion>> ListVersionsAsync(Guid definitionId, CancellationToken ct = default);
    Task<ReportTemplateVersion> CreateVersionAsync(ReportTemplateVersion version, CancellationToken ct = default);
}
