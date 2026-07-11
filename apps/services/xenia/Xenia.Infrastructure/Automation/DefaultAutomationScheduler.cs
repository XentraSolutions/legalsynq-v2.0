using System.Collections.Concurrent;
using Microsoft.Extensions.Options;
using Xenia.Application.Automation;
using Xenia.Domain.Automation;

namespace Xenia.Infrastructure.Automation;

/// <summary>
/// Phase 1 scheduler stub.
/// Scheduling is disabled by default — enabled via XeniaAutomationOptions.SchedulingEnabled.
/// Schedules are held in-memory; persistence is Phase H.
/// </summary>
internal sealed class DefaultAutomationScheduler : IAutomationScheduler
{
    private readonly ConcurrentDictionary<string, AutomationScheduleDefinition> _schedules = new(StringComparer.OrdinalIgnoreCase);
    private readonly XeniaAutomationOptions _opts;

    public DefaultAutomationScheduler(IOptions<XeniaAutomationOptions> opts) => _opts = opts.Value;

    public bool IsSchedulingEnabled => _opts.SchedulingEnabled;

    public Task<AutomationScheduleDefinition?> GetScheduleAsync(string automationKey, Guid? tenantId, CancellationToken ct = default)
    {
        _schedules.TryGetValue(ScopeKey(automationKey, tenantId), out var s);
        return Task.FromResult(s);
    }

    public Task<IReadOnlyList<AutomationScheduleDefinition>> GetAllSchedulesAsync(Guid? tenantId, CancellationToken ct = default)
    {
        IReadOnlyList<AutomationScheduleDefinition> result = [.. _schedules.Values];
        return Task.FromResult(result);
    }

    public Task<bool> SetScheduleAsync(AutomationScheduleDefinition schedule, Guid? tenantId, CancellationToken ct = default)
    {
        _schedules[ScopeKey(schedule.AutomationKey, tenantId)] = schedule;
        return Task.FromResult(true);
    }

    public Task<bool> DisableScheduleAsync(string automationKey, Guid? tenantId, CancellationToken ct = default)
    {
        var key = ScopeKey(automationKey, tenantId);
        if (!_schedules.TryGetValue(key, out var s)) return Task.FromResult(false);
        _schedules[key] = s with { IsEnabled = false };
        return Task.FromResult(true);
    }

    public Task<IReadOnlyList<AutomationScheduleDefinition>> GetDueSchedulesAsync(DateTime asOf, CancellationToken ct = default)
    {
        if (!IsSchedulingEnabled)
            return Task.FromResult<IReadOnlyList<AutomationScheduleDefinition>>([]);

        IReadOnlyList<AutomationScheduleDefinition> due = [.. _schedules.Values
            .Where(s => s.IsEnabled && s.NextScheduledAt.HasValue && s.NextScheduledAt.Value <= asOf)];
        return Task.FromResult(due);
    }

    private static string ScopeKey(string automationKey, Guid? tenantId) =>
        tenantId.HasValue ? $"{automationKey}::{tenantId}" : automationKey;
}

public sealed class XeniaAutomationOptions
{
    public const string Section = "XeniaAutomation";
    public bool SchedulingEnabled { get; init; } = false;
    public int MaxConcurrentExecutions { get; init; } = 10;
    public TimeSpan DefaultExecutionTimeout { get; init; } = TimeSpan.FromMinutes(5);
    public int MaxDeadLetterRetries { get; init; } = 3;
}
