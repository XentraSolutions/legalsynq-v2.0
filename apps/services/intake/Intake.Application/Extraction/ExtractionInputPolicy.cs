namespace Intake.Application.Extraction;

public static class ExtractionInputPolicy
{
    public static string BuildSafeValue(string? value, int maxCharacters) =>
        string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim().Length <= maxCharacters
                ? value.Trim()
                : value.Trim()[..maxCharacters];

    public static IReadOnlyList<string> BuildSafeEvidence(
        IEnumerable<string>? evidence,
        int maxItems,
        int maxCharacters) =>
        (evidence ?? [])
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => BuildSafeValue(item, maxCharacters))
            .Where(item => item.Length > 0)
            .Take(maxItems)
            .ToArray();
}