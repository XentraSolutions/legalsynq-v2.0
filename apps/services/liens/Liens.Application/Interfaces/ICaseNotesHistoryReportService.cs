using Liens.Application.DTOs;

namespace Liens.Application.Interfaces;

public interface ICaseNotesHistoryReportService
{
    Task<bool> IsLegacyHistoryReadyAsync(Guid tenantId, CancellationToken ct = default);

    Task<CaseNotesHistoryPage> GetAsync(
        Guid tenantId,
        CaseNotesHistoryQuery query,
        CancellationToken ct = default);

    Task<CaseNotesHistoryExport> ExportCsvAsync(
        Guid tenantId,
        CaseNotesHistoryQuery query,
        CancellationToken ct = default);
}
