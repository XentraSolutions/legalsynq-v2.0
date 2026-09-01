namespace Liens.Domain.Enums;

public static class CaseNoteCategory
{
    public const string General  = "general";
    public const string Feed = "feed";
    public const string Internal = "internal";
    public const string FollowUp = "follow-up";
    public const string CaseCreated = "Case Created";
    public const string SettlementHistory = "Settlement History";

    public static readonly IReadOnlySet<string> All =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            General,
            Feed,
            Internal,
            FollowUp,
            CaseCreated,
            SettlementHistory,
        };

    public static string? Normalize(string? category)
    {
        if (string.IsNullOrWhiteSpace(category))
            return null;

        var trimmed = category.Trim();
        return All.FirstOrDefault(candidate =>
            string.Equals(candidate, trimmed, StringComparison.OrdinalIgnoreCase));
    }
}
