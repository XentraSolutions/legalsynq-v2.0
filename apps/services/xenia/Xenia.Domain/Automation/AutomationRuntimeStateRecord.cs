using Xenia.Domain.Common;

namespace Xenia.Domain.Automation;

/// <summary>
/// Durable per-tenant runtime state for an automation.
///
/// Tracks lifecycle, health, failure counts, and scheduling eligibility.
/// Supports full round-trip persistence of the <see cref="AutomationRuntimeState"/> domain model.
///
/// Rules:
/// - No raw stack traces stored.
/// - No raw payloads or credentials.
/// - Error summary is bounded and categorized (safe).
/// - Unique per (TenantId, AutomationKey).
/// - Guid.Empty used as sentinel for global (platform-level, no-tenant) state.
/// </summary>
public sealed class AutomationRuntimeStateRecord : AuditableEntityBase
{
    public const int AutomationKeyMaxLength        = 200;
    public const int AutomationVersionMaxLength    = 50;
    public const int LifecycleStateMaxLength       = 50;
    public const int HealthStateMaxLength          = 50;
    public const int ErrorCategoryMaxLength        = 100;
    public const int ErrorSummaryMaxLength         = 500;
    public const int WorkerInstanceIdMaxLength     = 200;

    private AutomationRuntimeStateRecord() { }

    public Guid Id { get; private set; }

    /// <summary>Guid.Empty sentinel = global (no-tenant) platform-level state.</summary>
    public Guid TenantId { get; private set; }

    public string AutomationKey { get; private set; } = string.Empty;

    /// <summary>Current version string of the registered automation.</summary>
    public string AutomationVersion { get; private set; } = string.Empty;

    /// <summary>Platform-level global enable/disable state.</summary>
    public AutomationLifecycleState GlobalState { get; private set; }

    /// <summary>Tenant-specific override state. Null = no tenant override.</summary>
    public AutomationLifecycleState? TenantState { get; private set; }

    public AutomationLifecycleState LifecycleState { get; private set; }

    public AutomationHealthState HealthState { get; private set; }

    public DateTime? LastExecutionAt { get; private set; }
    public DateTime? LastSuccessfulExecutionAt { get; private set; }

    /// <summary>Consecutive failure count. Reset to 0 on success.</summary>
    public int ConsecutiveFailureCount { get; private set; }

    /// <summary>Total executions ever started (cumulative, never decrements).</summary>
    public int TotalExecutions { get; private set; }

    /// <summary>Currently active (in-flight) execution count.</summary>
    public int ActiveExecutions { get; private set; }

    /// <summary>Total cumulative failure count (never decrements).</summary>
    public int TotalFailureCount { get; private set; }

    /// <summary>Next time this automation is eligible to execute (back-off / circuit-breaker).</summary>
    public DateTime? NextEligibleExecutionAt { get; private set; }

    /// <summary>Safe, categorized error label — no raw exception text.</summary>
    public string? LastSafeErrorCategory { get; private set; }

    /// <summary>Bounded safe error summary — no raw stack trace.</summary>
    public string? LastSafeErrorSummary { get; private set; }

    /// <summary>Which worker instance last updated this record.</summary>
    public string? WorkerInstanceId { get; private set; }

    public uint RowVersion { get; private set; }

    public static AutomationRuntimeStateRecord Create(
        Guid tenantId,
        string automationKey,
        string automationVersion,
        AutomationLifecycleState globalState = AutomationLifecycleState.Registered,
        string? workerInstanceId = null)
    {
        return new AutomationRuntimeStateRecord
        {
            Id                     = Guid.CreateVersion7(),
            TenantId               = tenantId,
            AutomationKey          = automationKey,
            AutomationVersion      = automationVersion,
            GlobalState            = globalState,
            TenantState            = null,
            LifecycleState         = globalState,
            HealthState            = AutomationHealthState.Unknown,
            ConsecutiveFailureCount = 0,
            TotalExecutions        = 0,
            ActiveExecutions       = 0,
            TotalFailureCount      = 0,
            WorkerInstanceId       = workerInstanceId,
            RowVersion             = 0,
        };
    }

    public void SyncFromDomainModel(AutomationRuntimeState state)
    {
        AutomationVersion      = state.AutomationVersion;
        GlobalState            = state.GlobalState;
        TenantState            = state.TenantState;
        LifecycleState         = state.EffectiveState;
        ActiveExecutions       = state.ActiveExecutions;
        TotalExecutions        = state.TotalExecutions;
        TotalFailureCount      = state.FailedExecutions;
        ConsecutiveFailureCount = state.FailedExecutions;
        LastExecutionAt        = state.LastExecutedAt;
        LastSuccessfulExecutionAt = state.LastSucceededAt;
        LastSafeErrorSummary   = state.LastSafeError;
        HealthState            = state.FailedExecutions == 0
            ? AutomationHealthState.Healthy
            : state.FailedExecutions >= 5
                ? AutomationHealthState.Critical
                : AutomationHealthState.Degraded;
        RowVersion++;
    }

    public AutomationRuntimeState ToDomainModel()
    {
        var tenantId = TenantId == Guid.Empty ? (Guid?)null : TenantId;
        return AutomationRuntimeState.Reconstitute(
            Id, AutomationKey, AutomationVersion, tenantId,
            GlobalState, TenantState,
            ActiveExecutions, TotalExecutions, TotalFailureCount,
            LastExecutionAt, LastSuccessfulExecutionAt, LastSafeErrorSummary,
            CreatedAtUtc, UpdatedAtUtc, RowVersion);
    }

    public void RecordSuccess(DateTime executedAt, string? workerInstanceId = null)
    {
        LastExecutionAt            = executedAt;
        LastSuccessfulExecutionAt  = executedAt;
        ConsecutiveFailureCount    = 0;
        HealthState                = AutomationHealthState.Healthy;
        LastSafeErrorCategory      = null;
        LastSafeErrorSummary       = null;
        NextEligibleExecutionAt    = null;
        WorkerInstanceId           = workerInstanceId;
        RowVersion++;
    }

    public void RecordFailure(
        DateTime executedAt,
        string safeErrorCategory,
        string safeErrorSummary,
        DateTime? nextEligibleAt = null,
        string? workerInstanceId = null)
    {
        LastExecutionAt         = executedAt;
        ConsecutiveFailureCount++;
        TotalFailureCount++;
        HealthState             = ConsecutiveFailureCount >= 5
            ? AutomationHealthState.Critical
            : AutomationHealthState.Degraded;
        LastSafeErrorCategory   = safeErrorCategory;
        LastSafeErrorSummary    = safeErrorSummary;
        NextEligibleExecutionAt = nextEligibleAt;
        WorkerInstanceId        = workerInstanceId;
        RowVersion++;
    }

    public void SetLifecycle(AutomationLifecycleState state)
    {
        LifecycleState = state;
        GlobalState    = state;
        RowVersion++;
    }
}
