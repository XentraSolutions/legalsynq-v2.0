namespace Xenia.Application.Email.Ingestion;

/// <summary>
/// Per-source distributed lock for synchronization runs.
///
/// Ensures only one sync run is active per (TenantId, SourceId) at any time.
/// Different sources may sync concurrently.
///
/// Note: the default in-process implementation is not durable across process restarts.
/// A DB-backed or Redis-backed implementation is required for production multi-instance deployment.
/// </summary>
public interface IEmailSourceSyncLock
{
    /// <summary>
    /// Attempts to acquire the lock for the given source.
    /// Returns a lease handle on success; null if the source is already locked.
    /// The lease must be disposed to release the lock.
    /// </summary>
    Task<IEmailSourceSyncLease?> TryAcquireAsync(Guid tenantId, Guid emailSourceId, CancellationToken ct = default);

    /// <summary>Returns whether a lock is currently held for the given source.</summary>
    bool IsLocked(Guid tenantId, Guid emailSourceId);
}

/// <summary>
/// A lease that represents ownership of the per-source sync lock.
/// Disposing the lease releases the lock.
/// </summary>
public interface IEmailSourceSyncLease : IAsyncDisposable
{
    Guid TenantId { get; }
    Guid EmailSourceId { get; }
    DateTime AcquiredAt { get; }
}
