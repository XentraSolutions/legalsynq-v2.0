using Microsoft.Extensions.Logging;
using Reports.Application.Audit;
using Reports.Application.Execution;
using Reports.Application.Execution.DTOs;
using Reports.Application.Export.DTOs;
using Reports.Application.Templates.DTOs;
using Reports.Contracts.Adapters;
using Reports.Contracts.Export;

namespace Reports.Application.Export;

public sealed class ReportExportService : IReportExportService
{
    private const long MaxFileSizeBytes = 10 * 1024 * 1024;

    private readonly IReportExecutionService _executionService;
    private readonly IEnumerable<IReportExporter> _exporters;
    private readonly IAuditAdapter _audit;
    private readonly ILogger<ReportExportService> _log;

    public ReportExportService(
        IReportExecutionService executionService,
        IEnumerable<IReportExporter> exporters,
        IAuditAdapter audit,
        ILogger<ReportExportService> log)
    {
        _executionService = executionService;
        _exporters = exporters;
        _audit = audit;
        _log = log;
    }

    public async Task<ServiceResult<ExportReportResponse>> ExportReportAsync(
        ExportReportRequest request, CancellationToken ct)
    {
        var validation = ValidateRequest(request);
        if (validation is not null)
            return ServiceResult<ExportReportResponse>.BadRequest(validation);

        var exporter = _exporters.FirstOrDefault(e =>
            string.Equals(e.FormatName, request.Format.ToString(), StringComparison.OrdinalIgnoreCase));

        if (exporter is null)
            return ServiceResult<ExportReportResponse>.BadRequest(
                $"Unsupported export format: {request.Format}");

        var exportId = Guid.NewGuid();

        await TryAuditAsync(AuditEventFactory.ExportStarted(
            request.TenantId, request.RequestedByUserId, exportId,
            request.TemplateId, request.Format.ToString(), request.ProductCode));

        var executeRequest = new ExecuteReportRequest
        {
            TenantId = request.TenantId,
            TemplateId = request.TemplateId,
            ProductCode = request.ProductCode,
            OrganizationType = request.OrganizationType,
            ParametersJson = request.ParametersJson,
            RequestedByUserId = request.RequestedByUserId,
            UseOverride = request.UseOverride
        };

        var executionResult = await _executionService.ExecuteReportAsync(executeRequest, ct);

        if (!executionResult.Success || executionResult.Data is null)
        {
            var reason = executionResult.ErrorMessage ?? "Execution failed.";
            await TryAuditAsync(AuditEventFactory.ExportFailed(
                request.TenantId, request.RequestedByUserId, exportId,
                request.TemplateId, request.Format.ToString(), reason, request.ProductCode));

            return ServiceResult<ExportReportResponse>.Fail(
                $"Report execution failed: {reason}", executionResult.StatusCode);
        }

        var execData = executionResult.Data;

        var resultSet = new TabularResultSet
        {
            Columns = execData.Columns.Select(c => new TabularColumn
            {
                Key = c.Key,
                Label = c.Label,
                DataType = c.DataType,
                Order = c.Order
            }).ToList(),
            Rows = execData.Rows.Select(r => r.Values).ToList(),
            TotalRowCount = execData.RowCount,
            WasTruncated = false
        };

        var exportCtx = new ExportContext
        {
            TemplateCode = execData.TemplateCode,
            TemplateName = execData.TemplateName,
            TenantId = request.TenantId,
            GeneratedAtUtc = DateTimeOffset.UtcNow
        };

        ExportResult exportResult;
        try
        {
            exportResult = await exporter.ExportAsync(resultSet, exportCtx, ct);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Export {ExportId} failed during {Format} generation", exportId, request.Format);
            await TryAuditAsync(AuditEventFactory.ExportFailed(
                request.TenantId, request.RequestedByUserId, exportId,
                request.TemplateId, request.Format.ToString(), ex.Message, request.ProductCode));

            return ServiceResult<ExportReportResponse>.Fail(
                $"Export generation failed: {ex.Message}");
        }

        if (exportResult.FileSize > MaxFileSizeBytes)
        {
            var msg = $"Export file size ({exportResult.FileSize:N0} bytes) exceeds maximum ({MaxFileSizeBytes:N0} bytes).";
            _log.LogWarning("Export {ExportId}: {Message}", exportId, msg);
            await TryAuditAsync(AuditEventFactory.ExportFailed(
                request.TenantId, request.RequestedByUserId, exportId,
                request.TemplateId, request.Format.ToString(), msg, request.ProductCode));

            return ServiceResult<ExportReportResponse>.BadRequest(msg);
        }

        await TryAuditAsync(AuditEventFactory.ExportCompleted(
            request.TenantId, request.RequestedByUserId, exportId,
            request.TemplateId, request.Format.ToString(),
            execData.RowCount, exportResult.FileSize, request.ProductCode));

        _log.LogInformation(
            "Export completed: {ExportId} format={Format} rows={Rows} size={Size}",
            exportId, request.Format, execData.RowCount, exportResult.FileSize);

        return ServiceResult<ExportReportResponse>.Ok(new ExportReportResponse
        {
            ExportId = exportId,
            FileName = exportResult.FileName,
            ContentType = exportResult.ContentType,
            FileSize = exportResult.FileSize,
            GeneratedAtUtc = exportCtx.GeneratedAtUtc,
            Format = request.Format,
            Status = "Completed",
            FileContent = exportResult.FileContent
        });
    }

    private static string? ValidateRequest(ExportReportRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.TenantId))
            return "TenantId is required.";
        if (request.TemplateId == Guid.Empty)
            return "TemplateId is required.";
        if (string.IsNullOrWhiteSpace(request.ProductCode))
            return "ProductCode is required.";
        if (string.IsNullOrWhiteSpace(request.OrganizationType))
            return "OrganizationType is required.";
        if (string.IsNullOrWhiteSpace(request.RequestedByUserId))
            return "RequestedByUserId is required.";
        if (!Enum.IsDefined(typeof(ExportFormat), request.Format))
            return $"Invalid export format: {request.Format}";
        return null;
    }

    private async Task TryAuditAsync(Reports.Contracts.Audit.AuditEventDto auditEvent)
    {
        try
        {
            await _audit.RecordEventAsync(auditEvent);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Audit hook failed for action {Action}", auditEvent.EventType);
        }
    }
}
