namespace Xenia.Infrastructure.Assistant;

internal abstract class ProductAssistantToolApiSource
{
    internal const string AssistantToolApiPrefix = "/api/assistant-tools/";

    protected static string BuildAssistantToolPath(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
            throw new InvalidOperationException("Assistant tool API relative path is required.");

        var normalized = relativePath.Trim();
        while (normalized.StartsWith('/'))
            normalized = normalized[1..];

        return $"{AssistantToolApiPrefix}{normalized}";
    }

    protected static void EnsureAssistantToolPath(string path)
    {
        if (!path.StartsWith(AssistantToolApiPrefix, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Product assistant sources must call product-owned assistant tool APIs under '{AssistantToolApiPrefix}'.");
        }
    }
}
