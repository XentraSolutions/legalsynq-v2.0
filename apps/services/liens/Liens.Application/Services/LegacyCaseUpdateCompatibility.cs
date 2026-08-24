using Liens.Domain.Enums;

namespace Liens.Application.Services;

public static class LegacyCaseUpdateCompatibility
{
    public const string CaseTrackingNoteUpdateDescription = "Case Tracking Note Update";

    public static string NormalizeDescription(string content, string category)
    {
        if (!string.Equals(category, CaseNoteCategory.Internal, StringComparison.OrdinalIgnoreCase))
            return content;

        if (string.Equals(content, "Note updated", StringComparison.Ordinal))
            return CaseTrackingNoteUpdateDescription;

        const string legacySuffix = "; Note updated.";
        return content.StartsWith("Case updated:", StringComparison.Ordinal) &&
               content.EndsWith(legacySuffix, StringComparison.Ordinal)
            ? $"{content[..^legacySuffix.Length]}; {CaseTrackingNoteUpdateDescription}."
            : content;
    }
}
