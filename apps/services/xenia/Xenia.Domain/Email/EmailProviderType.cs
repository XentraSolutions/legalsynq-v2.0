namespace Xenia.Domain.Email;

/// <summary>
/// Supported email provider types.
///
/// Values are extensible — new providers can be added without schema changes
/// because this enum is stored as a string in the database.
/// </summary>
public enum EmailProviderType
{
    /// <summary>Microsoft 365 / Exchange Online via Microsoft Graph API or IMAP.</summary>
    Microsoft365,

    /// <summary>Google Workspace or Gmail via Gmail API or IMAP.</summary>
    Google,

    /// <summary>Generic IMAP provider.</summary>
    Imap,

    /// <summary>Generic POP3 provider.</summary>
    Pop3,

    /// <summary>Exchange Server accessed via IMAP protocol.</summary>
    ExchangeImap,
}
