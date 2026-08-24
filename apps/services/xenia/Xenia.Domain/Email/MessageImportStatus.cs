namespace Xenia.Domain.Email;

/// <summary>The result of importing a single message into Xenia.</summary>
public enum MessageImportStatus
{
    Pending   = 0,
    Imported  = 1,
    Updated   = 2,
    Duplicate = 3,
    Failed    = 4,
}
