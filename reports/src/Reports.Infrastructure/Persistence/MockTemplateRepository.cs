using Microsoft.Extensions.Logging;
using Reports.Contracts.Persistence;
using Reports.Domain.Entities;

namespace Reports.Infrastructure.Persistence;

public sealed class MockTemplateRepository : ITemplateRepository
{
    private readonly ILogger<MockTemplateRepository> _log;

    public MockTemplateRepository(ILogger<MockTemplateRepository> log) => _log = log;

    public Task<ReportTemplate?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        _log.LogDebug("MockTemplateRepository: GetById {Id}", id);
        return Task.FromResult<ReportTemplate?>(null);
    }

    public Task<ReportTemplate?> GetByCodeAsync(string code, CancellationToken ct)
    {
        _log.LogDebug("MockTemplateRepository: GetByCode {Code}", code);
        return Task.FromResult<ReportTemplate?>(null);
    }

    public Task<IReadOnlyList<ReportTemplate>> ListAsync(string? productCode, bool? activeOnly, int page, int pageSize, CancellationToken ct)
    {
        _log.LogDebug("MockTemplateRepository: List productCode={ProductCode}", productCode);
        return Task.FromResult<IReadOnlyList<ReportTemplate>>(Array.Empty<ReportTemplate>());
    }

    public Task<ReportTemplate> CreateAsync(ReportTemplate template, CancellationToken ct)
    {
        if (template.Id == Guid.Empty)
            template.Id = Guid.NewGuid();
        _log.LogDebug("MockTemplateRepository: Created {Id}", template.Id);
        return Task.FromResult(template);
    }

    public Task<ReportTemplate> UpdateAsync(ReportTemplate template, CancellationToken ct)
    {
        _log.LogDebug("MockTemplateRepository: Updated {Id}", template.Id);
        return Task.FromResult(template);
    }

    public Task<ReportTemplateVersion?> GetVersionAsync(Guid templateId, int versionNumber, CancellationToken ct)
    {
        _log.LogDebug("MockTemplateRepository: GetVersion {Id} v{Version}", templateId, versionNumber);
        return Task.FromResult<ReportTemplateVersion?>(null);
    }

    public Task<ReportTemplateVersion?> GetActiveVersionAsync(Guid templateId, CancellationToken ct)
    {
        _log.LogDebug("MockTemplateRepository: GetActiveVersion {Id}", templateId);
        return Task.FromResult<ReportTemplateVersion?>(null);
    }

    public Task<IReadOnlyList<ReportTemplateVersion>> ListVersionsAsync(Guid templateId, CancellationToken ct)
    {
        _log.LogDebug("MockTemplateRepository: ListVersions {Id}", templateId);
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
