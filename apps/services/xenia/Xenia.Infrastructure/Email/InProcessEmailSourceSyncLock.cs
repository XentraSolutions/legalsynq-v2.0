using System.Collections.Concurrent;
using Xenia.Application.Email.Ingestion;

namespace Xenia.Infrastructure.Email;

/// <summary>
/// In-process per-source sync lock using SemaphoreSlim.
///
/// Limitation: NOT durable across process restarts.
/// For multi-instance production deployment, replace with a DB-backed or Redis-backed lock.
/// This implementation is safe for single-process deployments (dev, single-instance staging).
/// </summary>
internal sealed class InProcessEmailSourceSyncLock : IEmailSourceSyncLock
{
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _semaphores = new();

    private static string Key(Guid tenantId, Guid emailSourceId) =>
        $"{tenantId:N}:{emailSourceId:N}";

    public bool IsLocked(Guid tenantId, Guid emailSourceId)
    {
        var key = Key(tenantId, emailSourceId);
        return _semaphores.TryGetValue(key, out var sem) && sem.CurrentCount == 0;
    }

    public async Task<IEmailSourceSyncLease?> TryAcquireAsync(
        Guid tenantId, Guid emailSourceId, CancellationToken ct = default)
    {
        var key = Key(tenantId, emailSourceId);
        var sem = _semaphores.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));

        var acquired = await sem.WaitAsync(0, ct);
        if (!acquired) return null;

        return new InProcessSyncLease(sem, tenantId, emailSourceId);
    }

    private sealed class InProcessSyncLease : IEmailSourceSyncLease
    {
        private readonly SemaphoreSlim _sem;
        private bool _released;

        public Guid TenantId { get; }
        public Guid EmailSourceId { get; }
        public DateTime AcquiredAt { get; } = DateTime.UtcNow;

        public InProcessSyncLease(SemaphoreSlim sem, Guid tenantId, Guid emailSourceId)
        {
            _sem         = sem;
            TenantId     = tenantId;
            EmailSourceId = emailSourceId;
        }

        public ValueTask DisposeAsync()
        {
            if (!_released)
            {
                _released = true;
                _sem.Release();
            }
            return ValueTask.CompletedTask;
        }
    }
}
