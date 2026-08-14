using System.Text.Json;
using Intake.Contracts.Emails;

namespace Intake.Application.Emails;

public static class InboundEmailCaptureSerialization
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false,
    };

    public static string SerializeReferences(IEnumerable<string> references) =>
        JsonSerializer.Serialize(
            references.Select(reference => reference.Trim()).ToArray(),
            JsonOptions);

    public static string SerializeHeaders(
        IEnumerable<InboundEmailHeaderInput> headers)
    {
        var safeHeaders = headers
            .Where(header => !IsSensitiveHeader(header.Name))
            .Select(header => new HeaderValue(
                header.Name.Trim(),
                (header.Values ?? []).ToArray()))
            .OrderBy(header => header.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(header => header.Name, StringComparer.Ordinal)
            .ToArray();

        return JsonSerializer.Serialize(safeHeaders, JsonOptions);
    }

    public static bool IsSensitiveHeader(string? name)
    {
        var normalized = name?.Trim() ?? string.Empty;
        return normalized.Equals("Authorization", StringComparison.OrdinalIgnoreCase) ||
               normalized.Equals("Proxy-Authorization", StringComparison.OrdinalIgnoreCase) ||
               normalized.Equals("Cookie", StringComparison.OrdinalIgnoreCase) ||
               normalized.Equals("Set-Cookie", StringComparison.OrdinalIgnoreCase) ||
               normalized.StartsWith("X-Auth", StringComparison.OrdinalIgnoreCase) ||
               normalized.StartsWith("X-Session", StringComparison.OrdinalIgnoreCase) ||
               normalized.StartsWith("X-Credential", StringComparison.OrdinalIgnoreCase) ||
               normalized.Contains("token", StringComparison.OrdinalIgnoreCase) ||
               normalized.Contains("secret", StringComparison.OrdinalIgnoreCase) ||
               normalized.Contains("password", StringComparison.OrdinalIgnoreCase) ||
               normalized.Contains("api-key", StringComparison.OrdinalIgnoreCase) ||
               normalized.Contains("apikey", StringComparison.OrdinalIgnoreCase);
    }

    private sealed record HeaderValue(string Name, IReadOnlyList<string> Values);
}