using Xenia.Domain.Common;

namespace Xenia.Domain.Automation;

/// <summary>
/// Durable schedule definition for a tenant automation.
///
/// Persists schedule definitions so they survive restart.
/// Schedule execution remains disabled (deferred to a future scheduler responsibility).
///
/// Rules:
/// - TimeZone must be a valid IANA or Windows timezone identifier.
/// - Expression (cron) and IntervalSeconds must be validated before persistence.
/// - No arbitrary code execution from schedule payload.
/// - No duplicate active schedule where ConcurrencyPolicy disallows it (enforced at service layer).
/// </summary>
public sealed class AutomationScheduleRecord : AuditableEntityBase
{
    public const int AutomationKeyMaxLength    = 200;
    public const int ScheduleTypeMaxLength     = 50;
    public const int ExpressionMaxLength       = 200;
    public const int TimeZoneMaxLength         = 100;
    public const int MisfirePolicyMaxLength    = 50;
    public const int ConcurrencyPolicyMaxLength = 50;
    public const int ActorMaxLength            = 200;

    private AutomationScheduleRecord() { }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public string AutomationKey { get; private set; } = string.Empty;

    public AutomationScheduleType ScheduleType { get; private set; }

    /// <summary>Cron expression (e.g. "0 * * * *"). Null for non-Cron types.</summary>
    public string? Expression { get; private set; }

    /// <summary>Interval in seconds. Null for non-Interval types.</summary>
    public int? IntervalSeconds { get; private set; }

    /// <summary>IANA or Windows timezone identifier.</summary>
    public string TimeZone { get; private set; } = "UTC";

    public bool Enabled { get; private set; }

    public DateTime? NextRunAt { get; private set; }
    public DateTime? LastRunAt { get; private set; }

    public AutomationMisfirePolicy MisfirePolicy { get; private set; }
    public AutomationConcurrencyPolicy ConcurrencyPolicy { get; private set; }

    public string? CreatedBy { get; private set; }
    public string? UpdatedBy { get; private set; }

    public uint RowVersion { get; private set; }

    public static AutomationScheduleRecord Create(
        Guid tenantId,
        string automationKey,
        AutomationScheduleType scheduleType,
        string timeZone,
        AutomationMisfirePolicy misfirePolicy,
        AutomationConcurrencyPolicy concurrencyPolicy,
        string? expression = null,
        int? intervalSeconds = null,
        DateTime? nextRunAt = null,
        string? createdBy = null)
    {
        return new AutomationScheduleRecord
        {
            Id                = Guid.CreateVersion7(),
            TenantId          = tenantId,
            AutomationKey     = automationKey,
            ScheduleType      = scheduleType,
            Expression        = expression,
            IntervalSeconds   = intervalSeconds,
            TimeZone          = timeZone,
            Enabled           = true,
            NextRunAt         = nextRunAt,
            MisfirePolicy     = misfirePolicy,
            ConcurrencyPolicy = concurrencyPolicy,
            CreatedBy         = createdBy,
            UpdatedBy         = createdBy,
            RowVersion        = 0,
        };
    }

    public void Enable(string? updatedBy = null)
    {
        Enabled   = true;
        UpdatedBy = updatedBy;
        RowVersion++;
    }

    public void Disable(string? updatedBy = null)
    {
        Enabled   = false;
        UpdatedBy = updatedBy;
        RowVersion++;
    }

    public void UpdateNextRun(DateTime nextRunAt, DateTime lastRunAt)
    {
        NextRunAt = nextRunAt;
        LastRunAt = lastRunAt;
        RowVersion++;
    }
}
