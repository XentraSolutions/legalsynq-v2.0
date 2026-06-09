namespace Commerce.Domain.Catalog;

/// <summary>
/// Catalog key normalization. All catalog item keys are stored and
/// compared in lowercase, trimmed form. This is a single source of
/// truth used by both Domain entities and Application validators.
/// </summary>
public static class CatalogKey
{
    public static string Normalize(string? key)
        => string.IsNullOrWhiteSpace(key) ? string.Empty : key.Trim().ToLowerInvariant();

    public static bool IsValid(string? key)
    {
        if (string.IsNullOrWhiteSpace(key)) return false;
        var k = key.Trim();
        if (k.Length is < 2 or > 64) return false;
        foreach (var c in k)
        {
            if (!(char.IsLetterOrDigit(c) || c is '-' or '_' or '.')) return false;
        }
        return true;
    }
}
