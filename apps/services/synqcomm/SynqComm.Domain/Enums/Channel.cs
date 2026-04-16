namespace SynqComm.Domain.Enums;

public static class Channel
{
    public const string InApp = "InApp";
    public const string SystemNote = "SystemNote";

    public static readonly IReadOnlyList<string> All = new[] { InApp, SystemNote };
}
