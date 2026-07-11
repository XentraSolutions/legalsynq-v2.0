using Xenia.Application.Automation.Models;
using Xenia.Domain.Automation;

namespace Xenia.Application.Automation;

/// <summary>
/// Manages registration, discovery, and lifecycle of automation providers.
///
/// Rules:
/// - Registration is idempotent for identical key+version.
/// - Duplicate key+version registration from a different provider is rejected.
/// - Enables/disables are global or tenant-scoped.
/// - Discovery returns all registered providers visible to the caller's context.
/// </summary>
public interface IAutomationRegistry
{
    Task<RegistrationResult> RegisterAsync(IAutomationProvider provider, CancellationToken ct = default);

    Task<IReadOnlyList<AutomationManifest>> GetAllManifestsAsync(Guid? tenantId, CancellationToken ct = default);
    Task<AutomationManifest?> GetManifestAsync(string automationKey, CancellationToken ct = default);

    Task<AutomationRuntimeState?> GetRuntimeStateAsync(string automationKey, Guid? tenantId, CancellationToken ct = default);

    Task<bool> EnableGloballyAsync(string automationKey, Guid actorId, CancellationToken ct = default);
    Task<bool> DisableGloballyAsync(string automationKey, Guid actorId, CancellationToken ct = default);
    Task<bool> EnableForTenantAsync(string automationKey, Guid tenantId, Guid actorId, CancellationToken ct = default);
    Task<bool> DisableForTenantAsync(string automationKey, Guid tenantId, Guid actorId, CancellationToken ct = default);

    Task<AutomationLifecycleState> GetEffectiveStateAsync(string automationKey, Guid? tenantId, CancellationToken ct = default);
    Task<IReadOnlyList<AutomationDependency>> GetDependenciesAsync(string automationKey, CancellationToken ct = default);

    IAutomationProvider? GetProvider(string automationKey);
    IReadOnlyList<IAutomationProvider> GetAllProviders();
}

public sealed record RegistrationResult(bool IsSuccess, bool WasDuplicate, string? ErrorMessage)
{
    public static RegistrationResult Success() => new(true, false, null);
    public static RegistrationResult Duplicate() => new(true, true, null);
    public static RegistrationResult Conflict(string message) => new(false, false, message);
}
