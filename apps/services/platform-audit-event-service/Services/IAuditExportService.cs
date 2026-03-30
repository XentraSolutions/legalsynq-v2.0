using PlatformAuditEventService.Authorization;
using PlatformAuditEventService.DTOs.Export;

namespace PlatformAuditEventService.Services;

/// <summary>
/// Orchestrates the full audit export lifecycle:
///
///   1. Validate and authorize the <see cref="DTOs.Export.ExportRequest"/>.
///   2. Persist an <see cref="Entities.AuditExportJob"/> in the Pending state.
///   3. Process the export (v1: synchronous in-request; future: background worker).
///   4. Write the output file via <see cref="Export.IExportStorageProvider"/>.
///   5. Transition the job to Completed or Failed and persist the result.
///   6. Return an <see cref="ExportStatusResponse"/> reflecting the terminal state.
///
/// Status polling is served by <see cref="GetStatusAsync"/>, which reads the
/// persisted job record without re-running the export logic.
/// </summary>
public interface IAuditExportService
{
    /// <summary>
    /// Submit a new export job.
    ///
    /// Authorization is performed using the provided <paramref name="caller"/> context
    /// (resolved by <c>QueryAuthMiddleware</c>). The same scope constraints applied
    /// to query endpoints are enforced here — cross-tenant requests and scope
    /// escalation are denied.
    ///
    /// v1 processes the export synchronously within the HTTP request. The job
    /// transitions through Pending → Processing → Completed (or Failed) before
    /// this method returns. The response reflects the terminal state.
    ///
    /// Throws <see cref="UnauthorizedAccessException"/> when the caller lacks
    /// permission for the requested scope. Callers should catch and return 403.
    /// </summary>
    Task<ExportStatusResponse> SubmitAsync(
        ExportRequest        request,
        IQueryCallerContext  caller,
        CancellationToken    ct = default);

    /// <summary>
    /// Return the current status of an existing export job.
    /// Returns null when no job with the given <paramref name="exportId"/> exists.
    /// </summary>
    Task<ExportStatusResponse?> GetStatusAsync(
        Guid              exportId,
        CancellationToken ct = default);
}
