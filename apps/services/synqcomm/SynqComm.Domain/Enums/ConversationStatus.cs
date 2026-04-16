namespace SynqComm.Domain.Enums;

public static class ConversationStatus
{
    public const string New = "New";
    public const string Open = "Open";
    public const string PendingInternal = "PendingInternal";
    public const string PendingExternal = "PendingExternal";
    public const string Resolved = "Resolved";
    public const string Closed = "Closed";
    public const string Archived = "Archived";

    public static readonly IReadOnlyList<string> All = new[]
    {
        New, Open, PendingInternal, PendingExternal, Resolved, Closed, Archived
    };

    public static readonly IReadOnlySet<string> Terminal = new HashSet<string>
    {
        Closed, Archived
    };
}
