using System.Text.RegularExpressions;

namespace Intake.Application.Classification;

public static class ClassificationInputPolicy
{
    private static readonly Regex PromptInjectionPattern = new(
        @"(?is)\b(ignore|disregard|override|forget)\b.{0,80}\b(previous|system|developer|instruction|rule)s?\b",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static string BuildBoundedDocumentText(string text, int maxCharacters)
    {
        if (maxCharacters < 256)
            throw new ArgumentOutOfRangeException(nameof(maxCharacters));

        var normalized = text
            .Replace("\0", string.Empty, StringComparison.Ordinal)
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Trim();

        if (normalized.Length > maxCharacters)
            normalized = normalized[..maxCharacters];

        // Document text is data, never instructions. Keep a marker rather than
        // silently presenting attacker-controlled text as part of the prompt.
        return PromptInjectionPattern.Replace(
            normalized,
            match => $"[UNTRUSTED_TEXT_REMOVED:{match.Value.Length}]");
    }

    public static IReadOnlyList<string> BuildSafeEvidence(
        IEnumerable<string> evidence)
    {
        return evidence
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Where(value => value.Length <= 160)
            .Take(3)
            .ToArray();
    }

    public static string? BuildSafeReason(string? reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
            return null;

        var normalized = reason
            .Replace("\0", string.Empty, StringComparison.Ordinal)
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Trim();
        if (normalized.Length > 500)
            normalized = normalized[..500];
        return PromptInjectionPattern.Replace(
            normalized,
            match => $"[UNTRUSTED_MODEL_TEXT_REMOVED:{match.Value.Length}]");
    }
}