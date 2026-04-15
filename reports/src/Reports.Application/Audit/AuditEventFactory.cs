using System.Text.Json;
using Reports.Contracts.Audit;
using Reports.Contracts.Context;

namespace Reports.Application.Audit;

public static class AuditEventFactory
{
    private const string SourceService = "reports-service";

    public static AuditEventDto TemplateCreated(string tenantId, string userId, Guid templateId, string templateCode, string? productCode, RequestContext? ctx = null)
        => Build("template.created", tenantId, userId, "ReportTemplate", templateId.ToString(), $"Template '{templateCode}' created", productCode, ctx,
            metadata: new { templateCode });

    public static AuditEventDto TemplateUpdated(string tenantId, string userId, Guid templateId, string templateCode, string? productCode, RequestContext? ctx = null)
        => Build("template.updated", tenantId, userId, "ReportTemplate", templateId.ToString(), $"Template '{templateCode}' updated", productCode, ctx,
            metadata: new { templateCode });

    public static AuditEventDto VersionCreated(string tenantId, string userId, Guid templateId, string templateCode, int versionNumber, string? productCode, RequestContext? ctx = null)
        => Build("version.created", tenantId, userId, "ReportTemplateVersion", templateId.ToString(), $"Version {versionNumber} created for template '{templateCode}'", productCode, ctx,
            metadata: new { templateCode, versionNumber });

    public static AuditEventDto VersionPublished(string tenantId, string userId, Guid templateId, string templateCode, int versionNumber, string? productCode, RequestContext? ctx = null)
        => Build("version.published", tenantId, userId, "ReportTemplateVersion", templateId.ToString(), $"Version {versionNumber} published for template '{templateCode}'", productCode, ctx,
            metadata: new { templateCode, versionNumber });

    public static AuditEventDto AssignmentCreated(string tenantId, string userId, Guid assignmentId, Guid templateId, string scope, RequestContext? ctx = null)
        => Build("template.assignment.created", tenantId, userId, "ReportTemplateAssignment", assignmentId.ToString(), $"Assignment created for template '{templateId}' scope '{scope}'", null, ctx,
            metadata: new { templateId, scope });

    public static AuditEventDto AssignmentUpdated(string tenantId, string userId, Guid assignmentId, Guid templateId, RequestContext? ctx = null)
        => Build("template.assignment.updated", tenantId, userId, "ReportTemplateAssignment", assignmentId.ToString(), $"Assignment updated for template '{templateId}'", null, ctx,
            metadata: new { templateId });

    public static AuditEventDto TenantCatalogResolved(string tenantId, string? productCode, int count, RequestContext? ctx = null)
        => Build("tenant.catalog.resolved", tenantId, "system", "TenantCatalog", tenantId, $"Tenant catalog resolved: {count} templates", productCode, ctx,
            metadata: new { count });

    public static AuditEventDto OverrideCreated(string tenantId, string userId, Guid overrideId, Guid templateId, RequestContext? ctx = null)
        => Build("tenant.override.created", tenantId, userId, "TenantReportOverride", overrideId.ToString(), $"Override created for template '{templateId}'", null, ctx,
            metadata: new { templateId });

    public static AuditEventDto OverrideReactivated(string tenantId, string userId, Guid overrideId, Guid templateId, RequestContext? ctx = null)
        => Build("tenant.override.reactivated", tenantId, userId, "TenantReportOverride", overrideId.ToString(), $"Override reactivated for template '{templateId}'", null, ctx,
            metadata: new { templateId });

    public static AuditEventDto OverrideUpdated(string tenantId, string userId, Guid overrideId, Guid templateId, RequestContext? ctx = null)
        => Build("tenant.override.updated", tenantId, userId, "TenantReportOverride", overrideId.ToString(), $"Override updated for template '{templateId}'", null, ctx,
            metadata: new { templateId });

    public static AuditEventDto OverrideDeactivated(string tenantId, string userId, Guid overrideId, Guid templateId, RequestContext? ctx = null)
        => Build("tenant.override.deactivated", tenantId, userId, "TenantReportOverride", overrideId.ToString(), $"Override deactivated for template '{templateId}'", null, ctx,
            metadata: new { templateId });

    public static AuditEventDto EffectiveReportResolved(string tenantId, Guid templateId, RequestContext? ctx = null)
        => Build("tenant.effective.report.resolved", tenantId, "system", "EffectiveReport", templateId.ToString(), $"Effective report resolved for template '{templateId}'", null, ctx);

    public static AuditEventDto ExecutionStarted(string tenantId, string userId, Guid executionId, Guid templateId, string templateCode, int versionNumber, string? productCode, RequestContext? ctx = null)
        => Build("report.execution.started", tenantId, userId, "ReportExecution", executionId.ToString(), $"Execution started for template '{templateCode}' v{versionNumber}", productCode, ctx,
            metadata: new { templateId, templateCode, versionNumber });

    public static AuditEventDto ExecutionCompleted(string tenantId, string userId, Guid executionId, Guid templateId, string templateCode, int rowCount, string? productCode, RequestContext? ctx = null)
        => Build("report.execution.completed", tenantId, userId, "ReportExecution", executionId.ToString(), $"Execution completed for template '{templateCode}' — {rowCount} rows", productCode, ctx,
            outcome: "Success", metadata: new { templateId, templateCode, rowCount });

    public static AuditEventDto ExecutionFailed(string tenantId, string userId, Guid executionId, Guid templateId, string templateCode, string reason, string? productCode, RequestContext? ctx = null)
        => Build("report.execution.failed", tenantId, userId, "ReportExecution", executionId.ToString(), $"Execution failed for template '{templateCode}': {reason}", productCode, ctx,
            outcome: "Failure", metadata: new { templateId, templateCode, reason });

    public static AuditEventDto ExportStarted(string tenantId, string userId, Guid exportId, Guid templateId, string format, string? productCode, RequestContext? ctx = null)
        => Build("report.export.started", tenantId, userId, "ReportExport", exportId.ToString(), $"Export started: template '{templateId}' format {format}", productCode, ctx,
            metadata: new { templateId, format });

    public static AuditEventDto ExportCompleted(string tenantId, string userId, Guid exportId, Guid templateId, string format, int rowCount, long fileSize, string? productCode, RequestContext? ctx = null)
        => Build("report.export.completed", tenantId, userId, "ReportExport", exportId.ToString(), $"Export completed: {rowCount} rows, {fileSize} bytes, format {format}", productCode, ctx,
            outcome: "Success", metadata: new { templateId, format, rowCount, fileSize });

    public static AuditEventDto ExportFailed(string tenantId, string userId, Guid exportId, Guid templateId, string format, string reason, string? productCode, RequestContext? ctx = null)
        => Build("report.export.failed", tenantId, userId, "ReportExport", exportId.ToString(), $"Export failed: {reason}", productCode, ctx,
            outcome: "Failure", metadata: new { templateId, format, reason });

    private static AuditEventDto Build(
        string eventType,
        string tenantId,
        string actorUserId,
        string entityType,
        string entityId,
        string description,
        string? productCode,
        RequestContext? ctx,
        string outcome = "Success",
        object? metadata = null)
    {
        return new AuditEventDto
        {
            EventType = eventType,
            OccurredAtUtc = DateTimeOffset.UtcNow,
            TenantId = tenantId,
            ProductCode = productCode,
            EntityType = entityType,
            EntityId = entityId,
            ActorUserId = actorUserId,
            CorrelationId = ctx?.CorrelationId,
            RequestId = ctx?.RequestId,
            Outcome = outcome,
            Action = eventType,
            Description = description,
            MetadataJson = metadata is not null ? JsonSerializer.Serialize(metadata) : null
        };
    }
}
