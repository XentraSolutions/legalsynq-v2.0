namespace Xenia.Domain.Automation;

/// <summary>
/// Current runtime state of an automation for a given tenant scope.
/// TenantId = null means platform-level global state.
/// </summary>
public sealed class AutomationRuntimeState
{
    public Guid Id { get; private set; }
    public required string AutomationKey { get; init; }
    public required string AutomationVersion { get; init; }
    public Guid? TenantId { get; private set; }
    public AutomationLifecycleState GlobalState { get; private set; }
    public AutomationLifecycleState? TenantState { get; private set; }
    public AutomationLifecycleState EffectiveState =>
        TenantState ?? GlobalState;
    public int ActiveExecutions { get; private set; }
    public int TotalExecutions { get; private set; }
    public int FailedExecutions { get; private set; }
    public DateTime? LastExecutedAt { get; private set; }
    public DateTime? LastSucceededAt { get; private set; }
    public string? LastSafeError { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }
    public uint RowVersion { get; private set; }

    private AutomationRuntimeState() { }

    /// <summary>
    /// Reconstitutes a domain model from persisted state (EF store → domain layer).
    /// Use only in EF store implementations; never call from application or domain logic.
    /// </summary>
    public static AutomationRuntimeState Reconstitute(
        Guid id,
        string automationKey,
        string automationVersion,
        Guid? tenantId,
        AutomationLifecycleState globalState,
        AutomationLifecycleState? tenantState,
        int activeExecutions,
        int totalExecutions,
        int failedExecutions,
        DateTime? lastExecutedAt,
        DateTime? lastSucceededAt,
        string? lastSafeError,
        DateTime createdAt,
        DateTime updatedAt,
        uint rowVersion)
    {
        return new AutomationRuntimeState
        {
            Id               = id,
            AutomationKey    = automationKey,
            AutomationVersion = automationVersion,
            TenantId         = tenantId,
            GlobalState      = globalState,
            TenantState      = tenantState,
            ActiveExecutions = activeExecutions,
            TotalExecutions  = totalExecutions,
            FailedExecutions = failedExecutions,
            LastExecutedAt   = lastExecutedAt,
            LastSucceededAt  = lastSucceededAt,
            LastSafeError    = lastSafeError,
            CreatedAt        = createdAt,
            UpdatedAt        = updatedAt,
            RowVersion       = rowVersion,
        };
    }

    public static AutomationRuntimeState Create(
        string automationKey,
        string automationVersion,
        Guid? tenantId,
        AutomationLifecycleState initialState = AutomationLifecycleState.Registered)
    {
        var now = DateTime.UtcNow;
        return new AutomationRuntimeState
        {
            Id               = Guid.CreateVersion7(),
            AutomationKey    = automationKey,
            AutomationVersion = automationVersion,
            TenantId         = tenantId,
            GlobalState      = initialState,
            TenantState      = null,
            ActiveExecutions = 0,
            TotalExecutions  = 0,
            FailedExecutions = 0,
            CreatedAt        = now,
            UpdatedAt        = now,
        };
    }

    public void SetGlobalState(AutomationLifecycleState state)
    {
        GlobalState = state;
        UpdatedAt   = DateTime.UtcNow;
    }

    public void SetTenantState(AutomationLifecycleState? state)
    {
        TenantState = state;
        UpdatedAt   = DateTime.UtcNow;
    }

    public void RecordExecutionStarted()
    {
        ActiveExecutions++;
        TotalExecutions++;
        UpdatedAt = DateTime.UtcNow;
    }

    public void RecordExecutionCompleted()
    {
        if (ActiveExecutions > 0) ActiveExecutions--;
        LastExecutedAt  = DateTime.UtcNow;
        LastSucceededAt = DateTime.UtcNow;
        UpdatedAt       = DateTime.UtcNow;
    }

    public void RecordExecutionFailed(string safeError)
    {
        if (ActiveExecutions > 0) ActiveExecutions--;
        FailedExecutions++;
        LastExecutedAt = DateTime.UtcNow;
        LastSafeError  = safeError;
        UpdatedAt      = DateTime.UtcNow;
    }
}
