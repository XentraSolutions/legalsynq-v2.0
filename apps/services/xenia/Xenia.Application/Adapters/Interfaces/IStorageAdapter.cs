namespace Xenia.Application.Adapters.Interfaces;

/// <summary>
/// Platform-neutral contract for object storage operations.
/// Xenia modules use this to store, retrieve, and delete binary objects
/// without depending on a specific storage provider (S3, Azure Blob, etc.).
/// </summary>
public interface IStorageAdapter
{
    /// <summary>Whether this adapter is configured for the current environment.</summary>
    bool IsConfigured { get; }

    /// <summary>
    /// Stores an object and returns its storage reference.
    /// Returns an unavailable result when the adapter is not configured.
    /// </summary>
    Task<StorageStoreResult> StoreAsync(
        string key,
        Stream content,
        string contentType,
        CancellationToken ct = default);

    /// <summary>
    /// Retrieves an object by its storage key.
    /// Returns null when not found or the adapter is unavailable.
    /// </summary>
    Task<StorageRetrieveResult?> RetrieveAsync(string key, CancellationToken ct = default);

    /// <summary>
    /// Deletes an object by its storage key.
    /// No-op when the object does not exist or the adapter is unavailable.
    /// </summary>
    Task<StorageDeleteResult> DeleteAsync(string key, CancellationToken ct = default);
}

public sealed record StorageStoreResult(bool IsStored, bool IsAvailable, string? StorageKey, string? Message);
public sealed record StorageRetrieveResult(string Key, Stream Content, string ContentType, long SizeBytes);
public sealed record StorageDeleteResult(bool IsDeleted, bool IsAvailable, string? Message);
