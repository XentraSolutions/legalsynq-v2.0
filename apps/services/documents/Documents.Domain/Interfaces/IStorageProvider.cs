namespace Documents.Domain.Interfaces;

public interface IStorageProvider
{
    string ProviderName { get; }

    Task<string> UploadAsync(
        string key,
        Stream content,
        string mimeType,
        CancellationToken ct = default);

    /// <summary>Download file as a readable stream. Caller is responsible for disposing.</summary>
    Task<Stream> DownloadAsync(string key, CancellationToken ct = default);

    /// <param name="downloadFileName">
    /// Filename to suggest via Content-Disposition when <paramref name="disposition"/> is
    /// "download". Falls back to the storage key's basename (a generated, non-descriptive
    /// name) when omitted.
    /// </param>
    Task<string> GenerateSignedUrlAsync(
        string key,
        int ttlSeconds,
        string disposition,
        string? downloadFileName = null,
        CancellationToken ct = default);

    Task DeleteAsync(string key, CancellationToken ct = default);
    Task<bool> ExistsAsync(string key, CancellationToken ct = default);
}
