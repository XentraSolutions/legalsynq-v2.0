namespace Xenia.Domain.Email;

/// <summary>The format of a stored message body.</summary>
public enum EmailMessageBodyType
{
    Unknown = 0,
    Plain   = 1,
    Html    = 2,
    Both    = 3,
}
