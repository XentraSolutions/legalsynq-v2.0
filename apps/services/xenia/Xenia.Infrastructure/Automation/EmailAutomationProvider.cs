using Microsoft.Extensions.Logging;
using Xenia.Application.Adapters.Interfaces;
using Xenia.Application.Automation;
using Xenia.Application.Automation.Models;
using Xenia.Application.Email.Ingestion;
using Xenia.Domain.Automation;

namespace Xenia.Infrastructure.Automation;

/// <summary>
/// IAutomationProvider implementation for the Xenia email ingestion module.
///
/// Exposes the email sync workflow as a generic automation to the platform registry.
/// The platform has no knowledge of email internals; it only sees the manifest,
/// dependencies, and execution results.
///
/// Platform-neutrality rules:
/// - No LegalSynq product-domain concepts in this class.
/// - No mention of liens, referrals, funding, etc.
/// - Dependency health is reported generically (Document, Notification, Audit adapters).
/// </summary>
internal sealed class EmailAutomationProvider : IAutomationProvider
{
    public string AutomationKey => "xenia.email.sync";
    public string Version => "1.0.0";
    public bool SupportsCancellation => false;

    private readonly IEmailSyncService _orchestrator;
    private readonly IDocumentAdapter _documentAdapter;
    private readonly IAuditAdapter _auditAdapter;
    private readonly INotificationAdapter _notificationAdapter;
    private readonly ILogger<EmailAutomationProvider> _logger;

    public EmailAutomationProvider(
        IEmailSyncService orchestrator,
        IDocumentAdapter documentAdapter,
        IAuditAdapter auditAdapter,
        INotificationAdapter notificationAdapter,
        ILogger<EmailAutomationProvider> logger)
    {
        _orchestrator        = orchestrator;
        _documentAdapter     = documentAdapter;
        _auditAdapter        = auditAdapter;
        _notificationAdapter = notificationAdapter;
        _logger              = logger;
    }

    public AutomationManifest GetManifest() => new()
    {
        AutomationKey          = AutomationKey,
        DisplayName            = "Email Ingestion Sync",
        Description            = "Periodically synchronises email from configured provider accounts and persists messages, recipients, and attachment references.",
        Version                = Version,
        Category               = "email",
        Provider               = "Xenia.Infrastructure.Email",
        Status                 = GetHealthState(),
        Capabilities           = AutomationCapability.Triggerable
                               | AutomationCapability.Schedulable
                               | AutomationCapability.ManualExecution
                               | AutomationCapability.SupportsRetry
                               | AutomationCapability.SupportsDiagnostics
                               | AutomationCapability.SupportsTenantConfiguration
                               | AutomationCapability.UsesDocuments
                               | AutomationCapability.UsesNotifications,
        Dependencies           = GetDependencies(),
        Permissions            = ["xenia.email.sync:execute", "xenia.email.source:read"],
        ConfigurationNamespace = "Xenia:Email",
        SupportedTriggers      = [AutomationTriggerType.Manual, AutomationTriggerType.Interval, AutomationTriggerType.Retry],
        SupportedExecutionModes = ["per-source", "batch"],
        TenantEnablementSupported = true,
        SchedulingSupported    = true,
        DiagnosticsSupported   = true,
        HealthSupported        = true,
        MinimumPlatformVersion = "1.0.0",
        MetadataVersion        = 1,
    };

    public AutomationLifecycleState GetHealthState()
    {
        var deps = GetDependencies();
        if (deps.Any(d => !d.IsOptional && d.AvailabilityState == DependencyAvailabilityState.Unavailable))
            return AutomationLifecycleState.Unavailable;
        if (deps.Any(d => d.AvailabilityState == DependencyAvailabilityState.Degraded))
            return AutomationLifecycleState.Degraded;
        return AutomationLifecycleState.Enabled;
    }

    public IReadOnlyList<AutomationDependency> GetDependencies() =>
    [
        new AutomationDependency
        {
            Key               = "document-adapter",
            DependencyType    = "platform-adapter",
            Criticality       = DependencyCriticality.Optional,
            AvailabilityState = _documentAdapter.IsConfigured
                ? DependencyAvailabilityState.Available
                : DependencyAvailabilityState.Disabled,
            ConfigurationState = _documentAdapter.IsConfigured ? "configured" : "not-configured",
            HealthImpact      = "Attachments will remain pending if document adapter is unavailable.",
        },
        new AutomationDependency
        {
            Key               = "audit-adapter",
            DependencyType    = "platform-adapter",
            Criticality       = DependencyCriticality.Recommended,
            AvailabilityState = _auditAdapter.IsConfigured
                ? DependencyAvailabilityState.Available
                : DependencyAvailabilityState.Disabled,
            ConfigurationState = _auditAdapter.IsConfigured ? "configured" : "not-configured",
            HealthImpact      = "Audit trail will be incomplete if audit adapter is unavailable.",
        },
        new AutomationDependency
        {
            Key               = "notification-adapter",
            DependencyType    = "platform-adapter",
            Criticality       = DependencyCriticality.Optional,
            AvailabilityState = _notificationAdapter.IsConfigured
                ? DependencyAvailabilityState.Available
                : DependencyAvailabilityState.Disabled,
            ConfigurationState = _notificationAdapter.IsConfigured ? "configured" : "not-configured",
            HealthImpact      = "Sync alerts will not be delivered if notification adapter is unavailable.",
        },
    ];

    public bool SupportsExecution(AutomationExecutionRequest request)
    {
        var mode = request.Parameters.GetValueOrDefault("mode", "per-source");
        return mode is "per-source" or "batch";
    }

    public async Task<AutomationExecutionResult> ExecuteAsync(
        AutomationExecutionRequest request,
        CancellationToken ct = default)
    {
        var executionId = request.Parameters.TryGetValue("executionId", out var eid)
            && Guid.TryParse(eid, out var g) ? g : Guid.CreateVersion7();
        var startedAt = DateTime.UtcNow;

        if (!request.Context.TenantId.HasValue)
        {
            return Fail(executionId, Version, startedAt, "MISSING_TENANT", "TenantId is required for email sync.");
        }

        if (!request.Parameters.TryGetValue("sourceId", out var sourceIdRaw)
            || !Guid.TryParse(sourceIdRaw, out var sourceId))
        {
            return Fail(executionId, Version, startedAt, "MISSING_SOURCE_ID", "sourceId parameter is required.");
        }

        try
        {
            var syncResult = await _orchestrator.RequestSyncAsync(
                request.Context.TenantId.Value,
                sourceId,
                request.Context.ActorId,
                request.Context.CorrelationId,
                ct);

            if (!syncResult.Accepted)
            {
                var category = syncResult.AlreadyRunning ? "ALREADY_RUNNING"
                    : syncResult.SourceNotFound        ? "SOURCE_NOT_FOUND"
                    : syncResult.SourceDisabled        ? "SOURCE_DISABLED"
                    : "SYNC_REJECTED";
                return Fail(executionId, Version, startedAt, category, $"Sync not accepted: {category}");
            }

            return new AutomationExecutionResult
            {
                ExecutionId      = executionId,
                AutomationKey    = AutomationKey,
                AutomationVersion = Version,
                Status           = AutomationExecutionStatus.Completed,
                StartedAt        = startedAt,
                CompletedAt      = DateTime.UtcNow,
                SafeMetadata     = new Dictionary<string, string>
                {
                    ["runId"]    = syncResult.RunId?.ToString() ?? string.Empty,
                    ["sourceId"] = sourceId.ToString(),
                },
            };
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "EmailAutomationProvider execution failed for sourceId={SourceId}", sourceId);
            return Fail(executionId, Version, startedAt, "EXECUTION_ERROR", "Email sync encountered an unexpected error.");
        }
    }

    public Task<bool> CancelAsync(Guid executionId, Guid? tenantId, CancellationToken ct = default) =>
        Task.FromResult(false);

    private static AutomationExecutionResult Fail(Guid id, string version, DateTime startedAt, string category, string summary) =>
        new()
        {
            ExecutionId      = id,
            AutomationKey    = "xenia.email.sync",
            AutomationVersion = version,
            Status           = AutomationExecutionStatus.Failed,
            StartedAt        = startedAt,
            CompletedAt      = DateTime.UtcNow,
            FailureCategory  = category,
            SafeErrorSummary = summary,
        };
}
