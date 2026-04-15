using Microsoft.Extensions.Logging;
using Reports.Application.Execution.DTOs;
using Reports.Application.Templates.DTOs;
using Reports.Contracts.Adapters;
using Reports.Contracts.Context;
using Reports.Contracts.Persistence;
using Reports.Domain.Entities;

namespace Reports.Application.Execution;

public sealed class ReportExecutionService : IReportExecutionService
{
    private const int MaxRowCap = 500;

    private readonly IReportRepository _executionRepo;
    private readonly ITemplateRepository _templateRepo;
    private readonly ITemplateAssignmentRepository _assignmentRepo;
    private readonly ITenantReportOverrideRepository _overrideRepo;
    private readonly IReportDataQueryAdapter _queryAdapter;
    private readonly IAuditAdapter _audit;
    private readonly ILogger<ReportExecutionService> _log;

    public ReportExecutionService(
        IReportRepository executionRepo,
        ITemplateRepository templateRepo,
        ITemplateAssignmentRepository assignmentRepo,
        ITenantReportOverrideRepository overrideRepo,
        IReportDataQueryAdapter queryAdapter,
        IAuditAdapter audit,
        ILogger<ReportExecutionService> log)
    {
        _executionRepo = executionRepo;
        _templateRepo = templateRepo;
        _assignmentRepo = assignmentRepo;
        _overrideRepo = overrideRepo;
        _queryAdapter = queryAdapter;
        _audit = audit;
        _log = log;
    }

    public async Task<ServiceResult<ReportExecutionResponse>> ExecuteReportAsync(
        ExecuteReportRequest request, CancellationToken ct)
    {
        var validation = ValidateRequest(request);
        if (validation is not null)
            return ServiceResult<ReportExecutionResponse>.BadRequest(validation);

        var tenantId = request.TenantId.Trim();
        var templateId = request.TemplateId;

        var template = await _templateRepo.GetByIdAsync(templateId, ct);
        if (template is null)
            return ServiceResult<ReportExecutionResponse>.NotFound($"Template '{templateId}' not found.");

        if (!template.IsActive)
            return ServiceResult<ReportExecutionResponse>.BadRequest($"Template '{templateId}' is not active.");

        if (!string.Equals(template.ProductCode, request.ProductCode.Trim(), StringComparison.OrdinalIgnoreCase))
            return ServiceResult<ReportExecutionResponse>.BadRequest(
                $"Product code mismatch: template product is '{template.ProductCode}', request specified '{request.ProductCode}'.");

        var isAssigned = await IsTenantAssignedAsync(templateId, tenantId, ct);
        if (!isAssigned)
            return ServiceResult<ReportExecutionResponse>.BadRequest(
                $"Template '{templateId}' is not assigned to tenant '{tenantId}'.");

        var publishedVersion = await _templateRepo.GetPublishedVersionAsync(templateId, ct);
        if (publishedVersion is null)
            return ServiceResult<ReportExecutionResponse>.NotFound(
                $"Template '{templateId}' has no published version.");

        if (!_queryAdapter.SupportsProduct(request.ProductCode.Trim()))
            return ServiceResult<ReportExecutionResponse>.BadRequest(
                $"Product '{request.ProductCode}' is not supported for report execution.");

        TenantReportOverride? activeOverride = null;
        if (request.UseOverride)
        {
            activeOverride = await _overrideRepo.GetByTenantAndTemplateAsync(tenantId, templateId, ct);
        }

        var definition = BuildExecutionDefinition(template, publishedVersion, activeOverride);

        var execution = new ReportExecution
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            UserId = request.RequestedByUserId.Trim(),
            ReportTemplateId = templateId,
            TemplateVersionNumber = publishedVersion.VersionNumber,
            Status = "Pending"
        };

        await _executionRepo.SaveAsync(execution, ct);

        await TryAuditAsync("report.execution.started",
            $"Execution '{execution.Id}' started for tenant '{tenantId}' template '{template.Code}' v{publishedVersion.VersionNumber}");

        execution.Status = "Running";
        await _executionRepo.UpdateAsync(execution, ct);

        try
        {
            var queryContext = new ReportQueryContext
            {
                TenantId = tenantId,
                ProductCode = template.ProductCode,
                TemplateId = templateId,
                TemplateCode = template.Code,
                OrganizationType = request.OrganizationType.Trim(),
                VersionNumber = publishedVersion.VersionNumber,
                TemplateBody = publishedVersion.TemplateBody,
                LayoutConfigJson = definition.LayoutConfigJson,
                ColumnConfigJson = definition.ColumnConfigJson,
                FilterConfigJson = definition.FilterConfigJson,
                ParametersJson = request.ParametersJson,
                MaxRows = MaxRowCap
            };

            var queryResult = await _queryAdapter.ExecuteQueryAsync(queryContext, ct);

            if (!queryResult.Success || queryResult.Data is null)
            {
                var reason = queryResult.ErrorMessage ?? "Query adapter returned no data.";
                execution.Status = "Failed";
                execution.FailureReason = reason;
                execution.CompletedAtUtc = DateTimeOffset.UtcNow;
                await _executionRepo.UpdateAsync(execution, ct);

                await TryAuditAsync("report.execution.failed",
                    $"Execution '{execution.Id}' failed: {reason}");

                return ServiceResult<ReportExecutionResponse>.Fail(
                    $"Report execution failed: {reason}");
            }

            var resultSet = queryResult.Data;

            execution.Status = "Completed";
            execution.CompletedAtUtc = DateTimeOffset.UtcNow;
            await _executionRepo.UpdateAsync(execution, ct);

            await TryAuditAsync("report.execution.completed",
                $"Execution '{execution.Id}' completed for tenant '{tenantId}' template '{template.Code}' — {resultSet.TotalRowCount} rows");

            _log.LogInformation(
                "Execution completed: {ExecutionId} tenant={TenantId} template={TemplateCode} rows={RowCount}",
                execution.Id, tenantId, template.Code, resultSet.TotalRowCount);

            var response = new ReportExecutionResponse
            {
                ExecutionId = execution.Id,
                TenantId = tenantId,
                TemplateId = templateId,
                TemplateCode = template.Code,
                TemplateName = definition.EffectiveName,
                PublishedVersionNumber = publishedVersion.VersionNumber,
                BaseTemplateVersionNumber = definition.BaseTemplateVersionNumber,
                HasOverride = definition.HasOverride,
                Columns = resultSet.Columns.Select(c => new ReportColumnResponse
                {
                    Key = c.Key,
                    Label = c.Label,
                    DataType = c.DataType,
                    Order = c.Order
                }).ToList(),
                Rows = resultSet.Rows.Select(r => new ReportRowResponse { Values = r }).ToList(),
                RowCount = resultSet.TotalRowCount,
                ExecutedAtUtc = execution.CompletedAtUtc!.Value,
                ExecutedByUserId = execution.UserId,
                Status = execution.Status
            };

            return ServiceResult<ReportExecutionResponse>.Ok(response);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Execution failed: {ExecutionId} tenant={TenantId} template={TemplateId}",
                execution.Id, tenantId, templateId);

            execution.Status = "Failed";
            execution.FailureReason = ex.Message;
            execution.CompletedAtUtc = DateTimeOffset.UtcNow;

            try { await _executionRepo.UpdateAsync(execution, ct); }
            catch (Exception updateEx) { _log.LogWarning(updateEx, "Failed to update execution status after failure"); }

            await TryAuditAsync("report.execution.failed",
                $"Execution '{execution.Id}' failed with exception: {ex.Message}");

            return ServiceResult<ReportExecutionResponse>.Fail(
                $"Report execution failed unexpectedly: {ex.Message}");
        }
    }

    public async Task<ServiceResult<ReportExecutionSummaryResponse>> GetExecutionByIdAsync(
        Guid executionId, CancellationToken ct)
    {
        var execution = await _executionRepo.GetByIdAsync(executionId, ct);
        if (execution is null)
            return ServiceResult<ReportExecutionSummaryResponse>.NotFound(
                $"Execution '{executionId}' not found.");

        return ServiceResult<ReportExecutionSummaryResponse>.Ok(new ReportExecutionSummaryResponse
        {
            ExecutionId = execution.Id,
            TenantId = execution.TenantId,
            TemplateId = execution.ReportTemplateId,
            TemplateVersionNumber = execution.TemplateVersionNumber,
            Status = execution.Status,
            FailureReason = execution.FailureReason,
            CreatedAtUtc = execution.CreatedAtUtc,
            CompletedAtUtc = execution.CompletedAtUtc
        });
    }

    private static ExecutionDefinition BuildExecutionDefinition(
        ReportTemplate template,
        ReportTemplateVersion publishedVersion,
        TenantReportOverride? activeOverride)
    {
        return new ExecutionDefinition
        {
            TemplateId = template.Id,
            TemplateCode = template.Code,
            EffectiveName = activeOverride?.NameOverride ?? template.Name,
            EffectiveDescription = activeOverride?.DescriptionOverride ?? template.Description,
            ProductCode = template.ProductCode,
            OrganizationType = template.OrganizationType,
            PublishedVersionNumber = publishedVersion.VersionNumber,
            TemplateBody = publishedVersion.TemplateBody,
            HasOverride = activeOverride is not null,
            BaseTemplateVersionNumber = activeOverride?.BaseTemplateVersionNumber,
            OverrideId = activeOverride?.Id,
            LayoutConfigJson = activeOverride?.LayoutConfigJson,
            ColumnConfigJson = activeOverride?.ColumnConfigJson,
            FilterConfigJson = activeOverride?.FilterConfigJson
        };
    }

    private async Task<bool> IsTenantAssignedAsync(Guid templateId, string tenantId, CancellationToken ct)
    {
        var hasGlobal = await _assignmentRepo.HasActiveGlobalAssignmentAsync(templateId, null, ct);
        if (hasGlobal) return true;

        var hasTenant = await _assignmentRepo.HasActiveTenantAssignmentAsync(templateId, tenantId, null, ct);
        return hasTenant;
    }

    private static string? ValidateRequest(ExecuteReportRequest request)
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
        return null;
    }

    private async Task TryAuditAsync(string action, string description)
    {
        try
        {
            var ctx = RequestContext.Default();
            var tenant = new TenantContext { TenantId = "system", IsActive = true };
            await _audit.RecordEventAsync(ctx, tenant, "system", action, description);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Audit hook failed for action {Action}", action);
        }
    }
}
