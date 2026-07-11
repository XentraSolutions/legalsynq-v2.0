using Xenia.Application.Automation;
using AppDiagEntry = Xenia.Application.Automation.AutomationRegistryEntry;
using Xenia.Domain.Automation;

namespace Xenia.Infrastructure.Automation;

internal sealed class DefaultAutomationDiagnosticsService : IAutomationDiagnosticsService
{
    private readonly IAutomationRegistry _registry;
    private readonly IAutomationDeadLetterStore _dlq;

    public DefaultAutomationDiagnosticsService(IAutomationRegistry registry, IAutomationDeadLetterStore dlq)
    {
        _registry = registry;
        _dlq      = dlq;
    }

    public async Task<AutomationDiagnosticsSnapshot> GetSnapshotAsync(Guid? tenantId, CancellationToken ct = default)
    {
        var manifests = await _registry.GetAllManifestsAsync(tenantId, ct);
        var dlqItems  = await _dlq.ListAsync(null, tenantId, AutomationDeadLetterStatus.Open, 1, 1000, ct);

        var registrations = new List<AutomationRegistryEntry>();
        foreach (var m in manifests)
        {
            var state = await _registry.GetRuntimeStateAsync(m.AutomationKey, tenantId, ct);
            registrations.Add(new AutomationRegistryEntry
            {
                AutomationKey    = m.AutomationKey,
                Version          = m.Version,
                Provider         = m.Provider,
                EffectiveState   = m.Status.ToString(),
                ActiveExecutions = state?.ActiveExecutions ?? 0,
                TotalExecutions  = state?.TotalExecutions ?? 0,
                FailedExecutions = state?.FailedExecutions ?? 0,
                LastExecutedAt   = state?.LastExecutedAt,
                LastSafeError    = state?.LastSafeError,
            });
        }

        var deps = manifests.SelectMany(m => m.Dependencies).Select(d => new AutomationDependencyStatus
        {
            Key               = d.Key,
            DependencyType    = d.DependencyType,
            Criticality       = d.Criticality.ToString(),
            AvailabilityState = d.AvailabilityState.ToString(),
            IsConfigured      = d.AvailabilityState != DependencyAvailabilityState.Unavailable,
        }).ToList();

        return new AutomationDiagnosticsSnapshot
        {
            GeneratedAt      = DateTime.UtcNow,
            ServiceVersion   = typeof(DefaultAutomationDiagnosticsService).Assembly.GetName().Version?.ToString() ?? "1.0.0",
            Environment      = System.Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production",
            Registrations    = registrations,
            Workers          = [],
            Dependencies     = deps,
            ActiveExecutions = registrations.Sum(r => r.ActiveExecutions),
            DeadLetterCount  = dlqItems.Count,
        };
    }

    public async Task<AutomationSupportBundle> GenerateSupportBundleAsync(Guid? tenantId, CancellationToken ct = default)
    {
        var diag = await GetSnapshotAsync(tenantId, ct);
        var cfgKeys = diag.Registrations
            .Select(r => $"Automation:{r.AutomationKey}:Enabled")
            .OrderBy(k => k)
            .ToList();

        var bundle = new AutomationSupportBundle
        {
            GeneratedAt      = diag.GeneratedAt,
            ServiceVersion   = diag.ServiceVersion,
            Environment      = diag.Environment,
            Diagnostics      = diag,
            ConfigurationKeyNames = cfgKeys,
            MigrationIds     = [],
            SafeSummary      = $"{diag.Registrations.Count} automations registered; {diag.ActiveExecutions} active; {diag.DeadLetterCount} dead-lettered.",
            BundleSizeEstimateBytes = 4096,
        };
        return bundle;
    }
}
