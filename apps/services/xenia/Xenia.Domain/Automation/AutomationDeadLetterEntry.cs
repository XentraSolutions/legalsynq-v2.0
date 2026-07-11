namespace Xenia.Domain.Automation;

/// <summary>
/// Persisted record of an automation execution that could not complete after bounded retries.
///
/// Safety rules:
/// - No secrets, tokens, or raw credentials stored.
/// - No raw provider payloads or message bodies.
/// - No raw headers or cursors.
/// - No attachment binaries.
/// </summary>
public sealed class AutomationDeadLetterEntry
{
    public Guid Id { get; private set; }
    public Guid? TenantId { get; private set; }
    public required string AutomationKey { get; init; }
    public required string AutomationVersion { get; init; }
    public Guid ExecutionId { get; private set; }
    public required AutomationTriggerType TriggerType { get; init; }
    public required string FailureCategory { get; init; }
    public required string SafeErrorSummary { get; init; }
    public int RetryCount { get; private set; }
    public DateTime FirstFailedAt { get; private set; }
    public DateTime LastFailedAt { get; private set; }
    public DateTime? NextEligibleRetryAt { get; private set; }
    public AutomationDeadLetterStatus Status { get; private set; }
    public string? CorrelationId { get; init; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    private AutomationDeadLetterEntry() { }

    public static AutomationDeadLetterEntry Create(
        Guid? tenantId,
        string automationKey,
        string automationVersion,
        Guid executionId,
        AutomationTriggerType triggerType,
        string failureCategory,
        string safeErrorSummary,
        string? correlationId,
        DateTime firstFailedAt)
    {
        var now = DateTime.UtcNow;
        return new AutomationDeadLetterEntry
        {
            Id               = Guid.CreateVersion7(),
            TenantId         = tenantId,
            AutomationKey    = automationKey,
            AutomationVersion = automationVersion,
            ExecutionId      = executionId,
            TriggerType      = triggerType,
            FailureCategory  = failureCategory,
            SafeErrorSummary = safeErrorSummary,
            RetryCount       = 0,
            FirstFailedAt    = firstFailedAt,
            LastFailedAt     = firstFailedAt,
            Status           = AutomationDeadLetterStatus.Open,
            CorrelationId    = correlationId,
            CreatedAt        = now,
            UpdatedAt        = now,
        };
    }

    public void RecordRetryAttempt(DateTime nextEligibleAt)
    {
        RetryCount++;
        LastFailedAt         = DateTime.UtcNow;
        NextEligibleRetryAt  = nextEligibleAt;
        Status               = AutomationDeadLetterStatus.Retrying;
        UpdatedAt            = DateTime.UtcNow;
    }

    public void MarkResolved()
    {
        Status    = AutomationDeadLetterStatus.Resolved;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Abandon()
    {
        Status    = AutomationDeadLetterStatus.Abandoned;
        UpdatedAt = DateTime.UtcNow;
    }
}
