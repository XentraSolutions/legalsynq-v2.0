using Microsoft.Extensions.Logging;
using Reports.Contracts.Persistence;
using Reports.Domain.Entities;

namespace Reports.Infrastructure.Persistence;

public sealed class MockTemplateRepository : ITemplateRepository
{
    private readonly ILogger<MockTemplateRepository> _log;

    public MockTemplateRepository(ILogger<MockTemplateRepository> log) => _log = log;

    public Task<ReportDefinition?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        _log.LogDebug("MockTemplateRepository: GetById {Id}", id);
        return Task.FromResult<ReportDefinition?>(null);
    }

    public Task<ReportDefinition?> GetByCodeAsync(string code, CancellationToken ct)
    {
        _log.LogDebug("MockTemplateRepository: GetByCode {Code}", code);
        return Task.FromResult<ReportDefinition?>(null);
    }

    public Task<IReadOnlyList<ReportDefinition>> ListAsync(string? productCode, bool? activeOnly, int page, int pageSize, CancellationToken ct)
    {
        _log.LogDebug("MockTemplateRepository: List productCode={ProductCode}", productCode);
        return Task.FromResult<IReadOnlyList<ReportDefinition>>(Array.Empty<ReportDefinition>());
    }

    public Task<ReportDefinition> CreateAsync(ReportDefinition definition, CancellationToken ct)
    {
        if (definition.Id == Guid.Empty)
            definition.Id = Guid.NewGuid();
        _log.LogDebug("MockTemplateRepository: Created {Id}", definition.Id);
        return Task.FromResult(definition);
    }

    public Task<ReportDefinition> UpdateAsync(ReportDefinition definition, CancellationToken ct)
    {
        _log.LogDebug("MockTemplateRepository: Updated {Id}", definition.Id);
        return Task.FromResult(definition);
    }

    public Task<ReportTemplateVersion?> GetVersionAsync(Guid definitionId, int versionNumber, CancellationToken ct)
    {
        _log.LogDebug("MockTemplateRepository: GetVersion {Id} v{Version}", definitionId, versionNumber);
        return Task.FromResult<ReportTemplateVersion?>(null);
    }

    public Task<ReportTemplateVersion?> GetActiveVersionAsync(Guid definitionId, CancellationToken ct)
    {
        _log.LogDebug("MockTemplateRepository: GetActiveVersion {Id}", definitionId);
        return Task.FromResult<ReportTemplateVersion?>(null);
    }

    public Task<IReadOnlyList<ReportTemplateVersion>> ListVersionsAsync(Guid definitionId, CancellationToken ct)
    {
        _log.LogDebug("MockTemplateRepository: ListVersions {Id}", definitionId);
        return Task.FromResult<IReadOnlyList<ReportTemplateVersion>>(Array.Empty<ReportTemplateVersion>());
    }

    public Task<ReportTemplateVersion> CreateVersionAsync(ReportTemplateVersion version, CancellationToken ct)
    {
        if (version.Id == Guid.Empty)
            version.Id = Guid.NewGuid();
        _log.LogDebug("MockTemplateRepository: Created version {Id}", version.Id);
        return Task.FromResult(version);
    }
}
