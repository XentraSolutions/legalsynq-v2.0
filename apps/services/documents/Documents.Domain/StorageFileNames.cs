namespace Documents.Domain;

/// <summary>
/// Sanitizes a caller-supplied download filename (derived from document Title, which is
/// user-controlled) before it is embedded in a Content-Disposition header or URL query
/// string, to prevent header/response splitting.
/// </summary>
public static class StorageFileNames
{
    public static string? Sanitize(string? fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName)) return null;

        var cleaned = fileName.Replace("\"", "'").Replace("\r", "").Replace("\n", "").Trim();
        return cleaned.Length == 0 ? null : cleaned;
    }

    /// <summary>
    /// Builds the filename to suggest for a document download: the document's title
    /// (its original filename, minus extension, for most upload paths) plus the real
    /// extension recorded in the storage key.
    /// </summary>
    public static string? ForDocument(string? title, string? storageKey)
    {
        var sanitizedTitle = Sanitize(title);
        if (sanitizedTitle is null) return null;

        var extension = string.IsNullOrEmpty(storageKey) ? "" : Path.GetExtension(storageKey);
        return $"{sanitizedTitle}{extension}";
    }
}
