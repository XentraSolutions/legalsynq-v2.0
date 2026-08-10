using Xenia.Application.Automation;
using Xenia.Application.Automation.Models;
using Xenia.Application.Adapters.Interfaces;
using Xenia.Domain.Automation;

namespace Xenia.Infrastructure.Automation;

/// <summary>
/// No-DB fallback implementations for automation services.
/// Registered when no XeniaDb connection string is configured so that
/// ASP.NET Core minimal-API endpoint mapping can always resolve these
/// services at startup (avoids "Body was inferred" pipeline-build crash).
/// All methods throw at runtime — callers receive 500 responses.
/// </summary>

internal sealed class UnavailableAutomationDiscoveryService : IAutomationDiscoveryService
{
    private static InvalidOperationException Unavailable() =>
        new("Automation discovery is not available without a database connection.");

    public Task<IReadOnlyList<AutomationManifest>> DiscoverAllAsync(Guid? tenantId, CancellationToken ct = default) =>
        throw Unavailable();

    public Task<AutomationManifest?> DiscoverByKeyAsync(string automationKey, CancellationToken ct = default) =>
        throw Unavailable();

    public Task<bool> IsAvailableAsync(string automationKey, Guid? tenantId, CancellationToken ct = default) =>
        throw Unavailable();
}

internal sealed class UnavailableAutomationRegistry : IAutomationRegistry
{
    private static InvalidOperationException Unavailable() =>
        new("Automation registry is not available without a database connection.");

    public Task<RegistrationResult> RegisterAsync(IAutomationProvider provider, CancellationToken ct = default) =>
        throw Unavailable();

    public Task<IReadOnlyList<AutomationManifest>> GetAllManifestsAsync(Guid? tenantId, CancellationToken ct = default) =>
        throw Unavailable();

    public Task<AutomationManifest?> GetManifestAsync(string automationKey, CancellationToken ct = default) =>
        throw Unavailable();

    public Task<AutomationRuntimeState?> GetRuntimeStateAsync(string automationKey, Guid? tenantId, CancellationToken ct = default) =>
        throw Unavailable();

    public Task<bool> EnableGloballyAsync(string automationKey, Guid actorId, CancellationToken ct = default) =>
        throw Unavailable();

    public Task<bool> DisableGloballyAsync(string automationKey, Guid actorId, CancellationToken ct = default) =>
        throw Unavailable();

    public Task<bool> EnableForTenantAsync(string automationKey, Guid tenantId, Guid actorId, CancellationToken ct = default) =>
        throw Unavailable();

    public Task<bool> DisableForTenantAsync(string automationKey, Guid tenantId, Guid actorId, CancellationToken ct = default) =>
        throw Unavailable();

    public Task<AutomationLifecycleState> GetEffectiveStateAsync(string automationKey, Guid? tenantId, CancellationToken ct = default) =>
        throw Unavailable();

    public Task<IReadOnlyList<AutomationDependency>> GetDependenciesAsync(string automationKey, CancellationToken ct = default) =>
        throw Unavailable();

    public IAutomationProvider? GetProvider(string automationKey) => null;

    public IReadOnlyList<IAutomationProvider> GetAllProviders() =>
        Array.Empty<IAutomationProvider>();
}

internal sealed class UnavailableAutomationExecutionService : IAutomationExecutionService
{
    private static InvalidOperationException Unavailable() =>
        new("Automation execution is not available without a database connection.");

    public Task<AutomationExecutionResult> ExecuteAsync(AutomationExecutionRequest request, CancellationToken ct = default) =>
        throw Unavailable();

    public Task<bool> CancelAsync(string automationKey, Guid executionId, Guid? tenantId, CancellationToken ct = default) =>
        throw Unavailable();

    public Task<IReadOnlyList<AutomationExecutionMetadata>> GetExecutionHistoryAsync(string? automationKey, Guid? tenantId, int page, int pageSize, CancellationToken ct = default) =>
        throw Unavailable();

    public Task<AutomationExecutionMetadata?> GetExecutionAsync(Guid executionId, Guid? tenantId, CancellationToken ct = default) =>
        throw Unavailable();
}

internal sealed class UnavailableAutomationDeadLetterStore : IAutomationDeadLetterStore
{
    private static InvalidOperationException Unavailable() =>
        new("Automation dead-letter store is not available without a database connection.");

    public Task<AutomationDeadLetterEntry> CreateAsync(AutomationDeadLetterEntry entry, CancellationToken ct = default) =>
        throw Unavailable();

    public Task<AutomationDeadLetterEntry?> GetAsync(Guid id, Guid? tenantId, CancellationToken ct = default) =>
        throw Unavailable();

    public Task<IReadOnlyList<AutomationDeadLetterEntry>> ListAsync(string? automationKey, Guid? tenantId, AutomationDeadLetterStatus? status, int page, int pageSize, CancellationToken ct = default) =>
        throw Unavailable();

    public Task<bool> RetryAsync(Guid id, Guid? tenantId, DateTime nextEligibleAt, CancellationToken ct = default) =>
        throw Unavailable();

    public Task<bool> AbandonAsync(Guid id, Guid? tenantId, CancellationToken ct = default) =>
        throw Unavailable();

    public Task<bool> ResolveAsync(Guid id, Guid? tenantId, CancellationToken ct = default) =>
        throw Unavailable();
}

internal sealed class UnavailableAutomationDiagnosticsService : IAutomationDiagnosticsService
{
    private static InvalidOperationException Unavailable() =>
        new("Automation diagnostics are not available without a database connection.");

    public Task<AutomationDiagnosticsSnapshot> GetSnapshotAsync(Guid? tenantId, CancellationToken ct = default) =>
        throw Unavailable();

    public Task<AutomationSupportBundle> GenerateSupportBundleAsync(Guid? tenantId, CancellationToken ct = default) =>
        throw Unavailable();
}

internal sealed class UnavailableAutomationScheduler : IAutomationScheduler
{
    private static InvalidOperationException Unavailable() =>
        new("Automation scheduling is not available without a database connection.");

    public bool IsSchedulingEnabled => false;

    public Task<AutomationScheduleDefinition?> GetScheduleAsync(string automationKey, Guid? tenantId, CancellationToken ct = default) =>
        throw Unavailable();

    public Task<IReadOnlyList<AutomationScheduleDefinition>> GetAllSchedulesAsync(Guid? tenantId, CancellationToken ct = default) =>
        throw Unavailable();

    public Task<bool> SetScheduleAsync(AutomationScheduleDefinition schedule, Guid? tenantId, CancellationToken ct = default) =>
        throw Unavailable();

    public Task<bool> DisableScheduleAsync(string automationKey, Guid? tenantId, CancellationToken ct = default) =>
        throw Unavailable();

    public Task<IReadOnlyList<AutomationScheduleDefinition>> GetDueSchedulesAsync(DateTime asOf, CancellationToken ct = default) =>
        throw Unavailable();
}
