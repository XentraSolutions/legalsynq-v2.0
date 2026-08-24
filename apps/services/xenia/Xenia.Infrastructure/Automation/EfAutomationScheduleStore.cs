using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xenia.Application.Automation;
using Xenia.Domain.Automation;
using Xenia.Infrastructure.Persistence;

namespace Xenia.Infrastructure.Automation;

/// <summary>
/// EF Core–backed implementation of <see cref="IAutomationScheduler"/>.
///
/// Replaces <see cref="DefaultAutomationScheduler"/>'s in-memory ConcurrentDictionary
/// with durable persistence to xn_automation_schedules.
///
/// Schedule execution remains disabled by default (SchedulingEnabled=false in options).
/// Definitions are persisted so they survive process restart.
///
/// Singleton — uses <see cref="IDbContextFactory{T}"/> per operation.
/// </summary>
internal sealed class EfAutomationScheduleStore : IAutomationScheduler
{
    private readonly IDbContextFactory<XeniaDbContext> _contextFactory;
    private readonly XeniaAutomationOptions _opts;
    private readonly ILogger<EfAutomationScheduleStore> _logger;

    public EfAutomationScheduleStore(
        IDbContextFactory<XeniaDbContext> contextFactory,
        IOptions<XeniaAutomationOptions> opts,
        ILogger<EfAutomationScheduleStore> logger)
    {
        _contextFactory = contextFactory;
        _opts           = opts.Value;
        _logger         = logger;
    }

    public bool IsSchedulingEnabled => _opts.SchedulingEnabled;

    public async Task<AutomationScheduleDefinition?> GetScheduleAsync(
        string automationKey, Guid? tenantId, CancellationToken ct = default)
    {
        var dbTenantId = ToDbTenantId(tenantId);
        await using var ctx = await _contextFactory.CreateDbContextAsync(ct);

        var record = await ctx.AutomationSchedules
            .AsNoTracking()
            .FirstOrDefaultAsync(s =>
                s.AutomationKey == automationKey &&
                s.TenantId == dbTenantId, ct);

        return record is null ? null : ToDefinition(record);
    }

    public async Task<IReadOnlyList<AutomationScheduleDefinition>> GetAllSchedulesAsync(
        Guid? tenantId, CancellationToken ct = default)
    {
        await using var ctx = await _contextFactory.CreateDbContextAsync(ct);

        IQueryable<AutomationScheduleRecord> query = ctx.AutomationSchedules.AsNoTracking();

        if (tenantId.HasValue)
            query = query.Where(s => s.TenantId == tenantId.Value);

        var records = await query.ToListAsync(ct);
        return records.ConvertAll(ToDefinition);
    }

    public async Task<bool> SetScheduleAsync(
        AutomationScheduleDefinition schedule, Guid? tenantId, CancellationToken ct = default)
    {
        var dbTenantId = ToDbTenantId(tenantId);
        await using var ctx = await _contextFactory.CreateDbContextAsync(ct);

        var existing = await ctx.AutomationSchedules
            .FirstOrDefaultAsync(s =>
                s.AutomationKey == schedule.AutomationKey &&
                s.TenantId == dbTenantId, ct);

        if (existing is null)
        {
            var scheduleType = DeriveScheduleType(schedule);
            var record = AutomationScheduleRecord.Create(
                dbTenantId,
                schedule.AutomationKey,
                scheduleType,
                timeZone: "UTC",
                misfirePolicy: AutomationMisfirePolicy.Skip,
                concurrencyPolicy: AutomationConcurrencyPolicy.SkipIfRunning,
                expression: schedule.CronExpression,
                intervalSeconds: ParseIntervalSeconds(schedule.IntervalExpression),
                nextRunAt: schedule.NextScheduledAt);

            if (!schedule.IsEnabled)
                record.Disable();

            ctx.AutomationSchedules.Add(record);
        }
        else
        {
            if (schedule.IsEnabled)
                existing.Enable();
            else
                existing.Disable();

            if (schedule.NextScheduledAt.HasValue && schedule.LastExecutedAt.HasValue)
                existing.UpdateNextRun(schedule.NextScheduledAt.Value, schedule.LastExecutedAt.Value);

            ctx.AutomationSchedules.Update(existing);
        }

        try
        {
            await ctx.SaveChangesAsync(ct);
            return true;
        }
        catch (DbUpdateException ex)
        {
            _logger.LogWarning(ex,
                "Failed to persist schedule for key={Key} tenant={TenantId}",
                schedule.AutomationKey, dbTenantId);
            return false;
        }
    }

    public async Task<bool> DisableScheduleAsync(
        string automationKey, Guid? tenantId, CancellationToken ct = default)
    {
        var dbTenantId = ToDbTenantId(tenantId);
        await using var ctx = await _contextFactory.CreateDbContextAsync(ct);

        var record = await ctx.AutomationSchedules
            .FirstOrDefaultAsync(s =>
                s.AutomationKey == automationKey &&
                s.TenantId == dbTenantId, ct);

        if (record is null) return false;

        record.Disable();

        try
        {
            await ctx.SaveChangesAsync(ct);
            return true;
        }
        catch (DbUpdateConcurrencyException ex)
        {
            _logger.LogWarning(ex,
                "Concurrency conflict disabling schedule key={Key} tenant={TenantId}",
                automationKey, dbTenantId);
            return false;
        }
    }

    public async Task<IReadOnlyList<AutomationScheduleDefinition>> GetDueSchedulesAsync(
        DateTime asOf, CancellationToken ct = default)
    {
        if (!IsSchedulingEnabled)
            return [];

        await using var ctx = await _contextFactory.CreateDbContextAsync(ct);

        var records = await ctx.AutomationSchedules
            .AsNoTracking()
            .Where(s => s.Enabled && s.NextRunAt.HasValue && s.NextRunAt.Value <= asOf)
            .ToListAsync(ct);

        return records.ConvertAll(ToDefinition);
    }

    // ── Private helpers ────────────────────────────────────────────────────────

    private static Guid ToDbTenantId(Guid? tenantId) => tenantId ?? Guid.Empty;

    private static AutomationScheduleDefinition ToDefinition(AutomationScheduleRecord r) =>
        new()
        {
            AutomationKey       = r.AutomationKey,
            TriggerType         = ToTriggerType(r.ScheduleType),
            CronExpression      = r.Expression,
            IntervalExpression  = r.IntervalSeconds.HasValue
                ? $"PT{r.IntervalSeconds.Value}S"
                : null,
            IsEnabled           = r.Enabled,
            NextScheduledAt     = r.NextRunAt,
            LastExecutedAt      = r.LastRunAt,
        };

    private static AutomationScheduleType DeriveScheduleType(AutomationScheduleDefinition def)
    {
        if (def.CronExpression is not null) return AutomationScheduleType.Cron;
        if (def.IntervalExpression is not null) return AutomationScheduleType.Interval;
        if (def.TriggerType == AutomationTriggerType.EventDriven) return AutomationScheduleType.EventDriven;
        if (def.TriggerType == AutomationTriggerType.Retry) return AutomationScheduleType.Retry;
        if (def.TriggerType == AutomationTriggerType.OneTime) return AutomationScheduleType.OneTime;
        return AutomationScheduleType.Manual;
    }

    private static AutomationTriggerType ToTriggerType(AutomationScheduleType scheduleType) =>
        scheduleType switch
        {
            AutomationScheduleType.Cron        => AutomationTriggerType.CronLike,
            AutomationScheduleType.Interval    => AutomationTriggerType.Interval,
            AutomationScheduleType.EventDriven => AutomationTriggerType.EventDriven,
            AutomationScheduleType.Retry       => AutomationTriggerType.Retry,
            AutomationScheduleType.OneTime     => AutomationTriggerType.OneTime,
            _                                  => AutomationTriggerType.Manual,
        };

    private static int? ParseIntervalSeconds(string? intervalExpression)
    {
        if (intervalExpression is null) return null;
        if (TimeSpan.TryParse(intervalExpression, out var ts))
            return (int)ts.TotalSeconds;
        if (intervalExpression.StartsWith("PT", StringComparison.OrdinalIgnoreCase) &&
            intervalExpression.EndsWith("S", StringComparison.OrdinalIgnoreCase) &&
            int.TryParse(intervalExpression[2..^1], out var secs))
            return secs;
        return null;
    }
}
