using Xenia.Application.Automation.Models;
using Xenia.Domain.Automation;

namespace Xenia.Application.Automation;

/// <summary>
/// Contract for automation providers registered with the Xenia platform.
///
/// Implementations must:
/// - Expose a provider-neutral manifest.
/// - Execute via the generic execution contract.
/// - Report health and dependencies without leaking internals.
/// - Remain platform-neutral (no LegalSynq product-domain logic).
///
/// Email is the first registered implementation; future modules follow the same contract.
/// </summary>
public interface IAutomationProvider
{
    string AutomationKey { get; }
    string Version { get; }

    AutomationManifest GetManifest();
    AutomationLifecycleState GetHealthState();
    IReadOnlyList<AutomationDependency> GetDependencies();

    Task<AutomationExecutionResult> ExecuteAsync(
        AutomationExecutionRequest request,
        CancellationToken ct = default);

    bool SupportsExecution(AutomationExecutionRequest request);
    bool SupportsCancellation { get; }

    Task<bool> CancelAsync(Guid executionId, Guid? tenantId, CancellationToken ct = default);
}
