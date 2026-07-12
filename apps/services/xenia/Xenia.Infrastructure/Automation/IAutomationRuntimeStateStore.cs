using Xenia.Domain.Automation;

namespace Xenia.Infrastructure.Automation;

/// <summary>
/// Internal persistence contract for automation runtime state.
/// Lives in Infrastructure to keep Application layer storage-agnostic.
/// </summary>
internal interface IAutomationRuntimeStateStore
{
    Task<AutomationRuntimeState?> GetAsync(string automationKey, Guid? tenantId, CancellationToken ct = default);
    Task UpsertAsync(AutomationRuntimeState state, CancellationToken ct = default);
    Task<IReadOnlyList<AutomationRuntimeState>> ListAsync(Guid? tenantId, CancellationToken ct = default);
}
