namespace Xenia.Domain.Email;

/// <summary>
/// Provider cursor and incremental sync state for one Email source.
///
/// One row per EmailSource. Owned by Xenia.
///
/// Rules:
/// - Cursor values must never appear in logs, audit events, or API responses.
/// - Cursor updates occur only after successful durable message persistence.
/// - Cursor resets must be audited.
/// - Invalidated cursors trigger a controlled initial re-sync.
/// </summary>
public sealed class EmailSyncState
{
    public const int CursorValueMaxLength     = 4000;
    public const int CursorMetadataMaxLength  = 2000;
    public const int ErrorCodeMaxLength       = 100;
    public const int SafeErrorSummaryMaxLength= 500;
    public const int SafeCursorSummaryMaxLength = 200;

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid EmailSourceId { get; private set; }
    public EmailProviderType ProviderType { get; private set; }
    public SyncCursorType CursorType { get; private set; }

    /// <summary>
    /// The durable cursor value (e.g. delta token, history ID, UID string).
    /// NEVER expose this in API responses or logs.
    /// For IMAP: "uidvalidity:lastuid", for POP3: serialized UIDL set.
    /// </summary>
    public string? CursorValue { get; private set; }

    /// <summary>Safe metadata about the cursor (no raw token). May include folder name, page hint.</summary>
    public string? CursorMetadataJson { get; private set; }

    /// <summary>Safe one-line description of the current cursor position. Safe to expose in UI.</summary>
    public string? SafeCursorSummary { get; private set; }

    public DateTime? LastSuccessfulSyncAt { get; private set; }
    public DateTime? LastAttemptedSyncAt { get; private set; }
    public DateTime? LastProcessedProviderTimestamp { get; private set; }
    public string? LastProcessedProviderMessageId { get; private set; }

    public bool InitialSyncCompleted { get; private set; }
    public int ConsecutiveFailureCount { get; private set; }
    public DateTime? NextEligibleSyncAt { get; private set; }

    public string? LastErrorCode { get; private set; }
    public string? SafeLastErrorSummary { get; private set; }

    /// <summary>Optimistic concurrency version — increment before each cursor commit.</summary>
    public int StateVersion { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    private EmailSyncState() { }

    public static EmailSyncState Create(Guid tenantId, Guid emailSourceId, EmailProviderType providerType)
    {
        return new EmailSyncState
        {
            Id                       = Guid.CreateVersion7(),
            TenantId                 = tenantId,
            EmailSourceId            = emailSourceId,
            ProviderType             = providerType,
            CursorType               = ProviderToCursorType(providerType),
            InitialSyncCompleted     = false,
            ConsecutiveFailureCount  = 0,
            StateVersion             = 0,
            CreatedAtUtc             = DateTime.UtcNow,
            UpdatedAtUtc             = DateTime.UtcNow,
        };
    }

    public void RecordAttempt()
    {
        LastAttemptedSyncAt = DateTime.UtcNow;
        UpdatedAtUtc        = DateTime.UtcNow;
    }

    public void CommitCursor(
        string? cursorValue,
        string? cursorMetadataJson,
        string? safeCursorSummary,
        DateTime? lastProcessedTimestamp,
        string? lastProcessedMessageId)
    {
        CursorValue                      = cursorValue;
        CursorMetadataJson               = cursorMetadataJson;
        SafeCursorSummary                = safeCursorSummary;
        LastProcessedProviderTimestamp   = lastProcessedTimestamp;
        LastProcessedProviderMessageId   = lastProcessedMessageId;
        LastSuccessfulSyncAt             = DateTime.UtcNow;
        InitialSyncCompleted             = true;
        ConsecutiveFailureCount          = 0;
        LastErrorCode                    = null;
        SafeLastErrorSummary             = null;
        StateVersion++;
        UpdatedAtUtc                     = DateTime.UtcNow;
    }

    public void RecordFailure(string errorCode, string safeErrorSummary, DateTime? nextEligibleAt)
    {
        ConsecutiveFailureCount++;
        LastErrorCode        = errorCode;
        SafeLastErrorSummary = safeErrorSummary;
        NextEligibleSyncAt   = nextEligibleAt;
        UpdatedAtUtc         = DateTime.UtcNow;
    }

    public void ResetCursor(string reason)
    {
        CursorValue              = null;
        CursorMetadataJson       = null;
        SafeCursorSummary        = $"Reset: {reason}";
        InitialSyncCompleted     = false;
        ConsecutiveFailureCount  = 0;
        NextEligibleSyncAt       = null;
        StateVersion++;
        UpdatedAtUtc             = DateTime.UtcNow;
    }

    public void ClearBackoff()
    {
        NextEligibleSyncAt = null;
        UpdatedAtUtc       = DateTime.UtcNow;
    }

    private static SyncCursorType ProviderToCursorType(EmailProviderType providerType) =>
        providerType switch
        {
            EmailProviderType.Microsoft365    => SyncCursorType.DeltaToken,
            EmailProviderType.Google => SyncCursorType.HistoryId,
            EmailProviderType.Imap            => SyncCursorType.ImapUidCursor,
            EmailProviderType.Pop3            => SyncCursorType.Pop3UidlSet,
            EmailProviderType.ExchangeImap    => SyncCursorType.ImapUidCursor,
            _                                 => SyncCursorType.ImapUidCursor,
        };
}
