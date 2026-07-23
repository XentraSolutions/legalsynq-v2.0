namespace Liens.Domain.Enums;

public static class CaseNoteCategory
{
    public const string General  = "general";
    public const string Feed = "feed";
    public const string Internal = "internal";
    public const string FollowUp = "follow-up";
    public const string CaseCreated = "Case Created";

    public static readonly IReadOnlySet<string> All =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            General,
            Feed,
            Internal,
            FollowUp,
            CaseCreated,
        };
}
