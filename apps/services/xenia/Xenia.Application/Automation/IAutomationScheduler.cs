using Xenia.Domain.Automation;

namespace Xenia.Application.Automation;

/// <summary>
/// Scheduling contracts for automation providers.
/// Phase 1: contracts + persistence only. Hosted scheduling is disabled by default.
/// Do not build a full distributed scheduler unless existing architecture supports it safely.
/// </summary>
public interface IAutomationScheduler
{
    bool IsSchedulingEnabled { get; }

    Task<AutomationScheduleDefinition?> GetScheduleAsync(string automationKey, Guid? tenantId, CancellationToken ct = default);
    Task<IReadOnlyList<AutomationScheduleDefinition>> GetAllSchedulesAsync(Guid? tenantId, CancellationToken ct = default);

    Task<bool> SetScheduleAsync(AutomationScheduleDefinition schedule, Guid? tenantId, CancellationToken ct = default);
    Task<bool> DisableScheduleAsync(string automationKey, Guid? tenantId, CancellationToken ct = default);
    Task<IReadOnlyList<AutomationScheduleDefinition>> GetDueSchedulesAsync(DateTime asOf, CancellationToken ct = default);
}
