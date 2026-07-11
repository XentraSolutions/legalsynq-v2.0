namespace Xenia.Domain.Email;

/// <summary>The addressing role of a message recipient.</summary>
public enum EmailRecipientType
{
    To      = 1,
    Cc      = 2,
    Bcc     = 3,
    ReplyTo = 4,
}
