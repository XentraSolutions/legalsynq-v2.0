namespace Xenia.Domain.Email;

/// <summary>
/// Records the result of a single retention execution for a tenant.
///
/// Retention runs are tenant-scoped and track all deletion/clearing counts.
/// Dry-run mode produces counts without making any changes.
///
/// Security: never stores credentials, message bodies, or raw cursors.
/// </summary>
public sealed class EmailRetentionRun
{
    public const int SafeErrorSummaryMaxLength = 500;
    public const int CorrelationIdMaxLength    = 200;

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }

    public EmailRetentionMode Mode { get; private set; }
    public EmailRetentionRunStatus Status { get; private set; }

    public DateTime StartedAt { get; private set; }
    public DateTime? CompletedAt { get; private set; }

    // ── Counts ────────────────────────────────────────────────────────────────
    public int MessagesEligible { get; private set; }
    public int MessagesDeleted { get; private set; }
    public int BodiesCleared { get; private set; }
    public int RunsDeleted { get; private set; }
    public int AlertsDeleted { get; private set; }
    public int AttachmentReferencesDeleted { get; private set; }
    public int Failures { get; private set; }

    public string? SafeErrorSummary { get; private set; }
    public string? CorrelationId { get; private set; }

    /// <summary>User or system actor that triggered the retention run.</summary>
    public Guid? ActorId { get; private set; }

    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    private EmailRetentionRun() { }

    public static EmailRetentionRun Create(
        Guid tenantId,
        EmailRetentionMode mode,
        Guid? actorId,
        string? correlationId)
    {
        var now = DateTime.UtcNow;
        return new EmailRetentionRun
        {
            Id            = Guid.CreateVersion7(),
            TenantId      = tenantId,
            Mode          = mode,
            Status        = EmailRetentionRunStatus.Running,
            StartedAt     = now,
            ActorId       = actorId,
            CorrelationId = correlationId,
            CreatedAt     = now,
            UpdatedAt     = now,
        };
    }

    public void RecordProgress(
        int messagesEligible,
        int messagesDeleted,
        int bodiesCleared,
        int runsDeleted,
        int alertsDeleted,
        int attachmentReferencesDeleted,
        int failures)
    {
        MessagesEligible              = messagesEligible;
        MessagesDeleted               = messagesDeleted;
        BodiesCleared                 = bodiesCleared;
        RunsDeleted                   = runsDeleted;
        AlertsDeleted                 = alertsDeleted;
        AttachmentReferencesDeleted   = attachmentReferencesDeleted;
        Failures                      = failures;
        UpdatedAt                     = DateTime.UtcNow;
    }

    public void Complete()
    {
        Status      = EmailRetentionRunStatus.Completed;
        CompletedAt = DateTime.UtcNow;
        UpdatedAt   = DateTime.UtcNow;
    }

    public void Fail(string safeErrorSummary)
    {
        Status          = EmailRetentionRunStatus.Failed;
        SafeErrorSummary= safeErrorSummary;
        CompletedAt     = DateTime.UtcNow;
        UpdatedAt       = DateTime.UtcNow;
    }

    public void Cancel()
    {
        Status      = EmailRetentionRunStatus.Cancelled;
        CompletedAt = DateTime.UtcNow;
        UpdatedAt   = DateTime.UtcNow;
    }
}
