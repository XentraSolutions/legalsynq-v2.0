using Xenia.Application.Adapters.Interfaces;

namespace Xenia.Infrastructure.Platform;

/// <summary>
/// Noop implementation of <see cref="IStorageAdapter"/>.
/// Returns honest unavailable results. Never reports false success.
/// </summary>
internal sealed class UnavailableStorageAdapter : IStorageAdapter
{
    private const string UnconfiguredMessage =
        "Storage adapter is not configured. Wire a real IStorageAdapter (e.g. S3) for production.";

    public bool IsConfigured => false;

    public Task<StorageStoreResult> StoreAsync(
        string key, Stream content, string contentType, CancellationToken ct = default)
        => Task.FromResult(new StorageStoreResult(
            IsStored: false, IsAvailable: false, StorageKey: null, Message: UnconfiguredMessage));

    public Task<StorageRetrieveResult?> RetrieveAsync(string key, CancellationToken ct = default)
        => Task.FromResult<StorageRetrieveResult?>(null);

    public Task<StorageDeleteResult> DeleteAsync(string key, CancellationToken ct = default)
        => Task.FromResult(new StorageDeleteResult(
            IsDeleted: false, IsAvailable: false, Message: UnconfiguredMessage));
}
