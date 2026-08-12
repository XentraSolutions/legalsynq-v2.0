namespace Xenia.Domain.Email;

/// <summary>The cursor mechanism used by a provider for incremental synchronization.</summary>
public enum SyncCursorType
{
    /// <summary>Microsoft 365 Graph delta query token.</summary>
    DeltaToken      = 1,

    /// <summary>Google Workspace Gmail history ID.</summary>
    HistoryId       = 2,

    /// <summary>IMAP UIDVALIDITY + last processed UID.</summary>
    ImapUidCursor   = 3,

    /// <summary>POP3 UIDL checkpoint set.</summary>
    Pop3UidlSet     = 4,
}
