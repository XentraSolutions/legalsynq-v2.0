using Liens.Application.DTOs;

namespace Liens.Application.Interfaces;

public interface ICaseNotesHistoryReportService
{
    Task<CaseNotesHistoryPage> GetAsync(
        Guid tenantId,
        CaseNotesHistoryQuery query,
        CancellationToken ct = default);

    Task<CaseNotesHistoryExport> ExportCsvAsync(
        Guid tenantId,
        CaseNotesHistoryQuery query,
        CancellationToken ct = default);
}
