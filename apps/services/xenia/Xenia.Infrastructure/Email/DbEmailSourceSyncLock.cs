using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xenia.Application.Email.Ingestion;
using Xenia.Domain.Email;
using Xenia.Infrastructure.Persistence;

namespace Xenia.Infrastructure.Email;

/// <summary>
/// Database-backed durable per-source synchronization lock.
///
/// Replaces <see cref="InProcessEmailSourceSyncLock"/> for production and multi-instance use.
/// Lock state is persisted in <c>xn_email_source_sync_locks</c> and survives process restarts.
///
/// Key behaviors:
/// - Unique constraint on (TenantId, EmailSourceId) — only one row per source.
/// - Atomic acquisition: first writer wins; concurrent writers get a duplicate-key exception.
/// - Lease expiration: expired locks are recoverable by any instance (crash safety).
/// - Optimistic concurrency (Version field) guards against simultaneous stale-lock takeover.
/// - Release sets ExpiresAt to the past — immediately available for the next run.
/// - Cross-tenant safety: TenantId is always part of the constraint — never cross-tenant.
/// </summary>
internal sealed class DbEmailSourceSyncLock : IEmailSourceSyncLock
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly XeniaIngestionOptions _opts;
    private readonly ILogger<DbEmailSourceSyncLock> _logger;

    public DbEmailSourceSyncLock(
        IServiceScopeFactory scopeFactory,
        IOptions<XeniaIngestionOptions> opts,
        ILogger<DbEmailSourceSyncLock> logger)
    {
        _scopeFactory = scopeFactory;
        _opts         = opts.Value;
        _logger       = logger;
    }

    public bool IsLocked(Guid tenantId, Guid emailSourceId)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<XeniaDbContext>();
        var row = db.EmailSourceSyncLocks
            .AsNoTracking()
            .FirstOrDefault(l => l.TenantId == tenantId && l.EmailSourceId == emailSourceId);
        return row is not null && !row.IsExpired;
    }

    public async Task<IEmailSourceSyncLease?> TryAcquireAsync(
        Guid tenantId, Guid emailSourceId, CancellationToken ct = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<XeniaDbContext>();

        var now      = DateTime.UtcNow;
        var ownerId  = BuildOwnerId();
        var expiresAt= now.Add(_opts.SourceLockLeaseDuration);

        var existing = await db.EmailSourceSyncLocks
            .FirstOrDefaultAsync(l => l.TenantId == tenantId && l.EmailSourceId == emailSourceId, ct);

        if (existing is not null && !existing.IsExpired)
        {
            _logger.LogDebug(
                "Lock held: tenantId={TenantId} sourceId={SourceId} by={Owner} expiresAt={ExpiresAt}",
                tenantId, emailSourceId, existing.LeaseOwnerId, existing.ExpiresAt);
            return null;
        }

        try
        {
            if (existing is null)
            {
                db.EmailSourceSyncLocks.Add(
                    EmailSourceSyncLock.Create(tenantId, emailSourceId, ownerId, expiresAt));
            }
            else
            {
                existing.Acquire(ownerId, expiresAt);
            }

            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (IsDuplicateKey(ex))
        {
            _logger.LogDebug(
                "Lock race lost: tenantId={TenantId} sourceId={SourceId}",
                tenantId, emailSourceId);
            return null;
        }
        catch (DbUpdateConcurrencyException)
        {
            _logger.LogDebug(
                "Lock concurrency conflict: tenantId={TenantId} sourceId={SourceId}",
                tenantId, emailSourceId);
            return null;
        }

        _logger.LogDebug(
            "Lock acquired: tenantId={TenantId} sourceId={SourceId} owner={Owner} expiresAt={ExpiresAt}",
            tenantId, emailSourceId, ownerId, expiresAt);

        return new DbSyncLease(_scopeFactory, tenantId, emailSourceId, ownerId, now, _logger);
    }

    private static string BuildOwnerId() =>
        $"{Environment.MachineName}-{Environment.ProcessId}-{Guid.CreateVersion7():N}";

    private static bool IsDuplicateKey(DbUpdateException ex) =>
        ex.InnerException?.Message.Contains("Duplicate entry", StringComparison.OrdinalIgnoreCase) == true
        || ex.InnerException?.Message.Contains("duplicate key", StringComparison.OrdinalIgnoreCase) == true
        || ex.InnerException?.Message.Contains("UNIQUE constraint", StringComparison.OrdinalIgnoreCase) == true;

    private sealed class DbSyncLease : IEmailSourceSyncLease
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger _logger;
        private bool _released;

        public Guid TenantId { get; }
        public Guid EmailSourceId { get; }
        public DateTime AcquiredAt { get; }
        public string LeaseOwnerId { get; }

        public DbSyncLease(
            IServiceScopeFactory scopeFactory,
            Guid tenantId,
            Guid emailSourceId,
            string leaseOwnerId,
            DateTime acquiredAt,
            ILogger logger)
        {
            _scopeFactory  = scopeFactory;
            TenantId       = tenantId;
            EmailSourceId  = emailSourceId;
            LeaseOwnerId   = leaseOwnerId;
            AcquiredAt     = acquiredAt;
            _logger        = logger;
        }

        public async ValueTask DisposeAsync()
        {
            if (_released) return;
            _released = true;

            try
            {
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<XeniaDbContext>();

                var row = await db.EmailSourceSyncLocks
                    .FirstOrDefaultAsync(l =>
                        l.TenantId     == TenantId
                        && l.EmailSourceId == EmailSourceId
                        && l.LeaseOwnerId  == LeaseOwnerId);

                if (row is not null)
                {
                    row.Release();
                    await db.SaveChangesAsync();
                    _logger.LogDebug(
                        "Lock released: tenantId={TenantId} sourceId={SourceId} owner={Owner}",
                        TenantId, EmailSourceId, LeaseOwnerId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Lock release failed — will expire naturally: tenantId={TenantId} sourceId={SourceId}",
                    TenantId, EmailSourceId);
            }
        }
    }
}
