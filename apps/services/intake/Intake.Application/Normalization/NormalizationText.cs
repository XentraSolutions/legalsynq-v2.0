using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Unicode;

namespace Intake.Application.Normalization;

internal static class NormalizationText
{
    public static string Display(string raw)
    {
        var normalized = raw.Normalize(System.Text.NormalizationForm.FormKC);
        var builder = new StringBuilder(normalized.Length);
        var whitespacePending = false;
        foreach (var character in normalized)
        {
            if (char.IsControl(character) && character is not '\t' and not '\r' and not '\n')
            {
                whitespacePending = true;
                continue;
            }

            if (char.IsWhiteSpace(character))
            {
                whitespacePending = builder.Length > 0;
                continue;
            }

            if (whitespacePending && builder.Length > 0)
                builder.Append(' ');
            whitespacePending = false;
            builder.Append(character);
        }

        return builder.ToString().Trim();
    }

    public static string ComparisonKey(string value)
    {
        var decomposed = value.Normalize(System.Text.NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (var character in decomposed)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(character);
            if (category == UnicodeCategory.NonSpacingMark)
                continue;
            if (char.IsLetterOrDigit(character))
                builder.Append(char.ToUpperInvariant(character));
        }

        return builder.ToString();
    }

    public static string Json(object value) =>
        JsonSerializer.Serialize(value, new JsonSerializerOptions(JsonSerializerDefaults.Web));

    public static string? Trimmed(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : Display(value);
}