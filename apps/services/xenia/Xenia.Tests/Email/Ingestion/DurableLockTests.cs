using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xenia.Application.Email.Ingestion;
using Xunit;
using Xenia.Domain.Email;
using Xenia.Infrastructure.Email;
using Xenia.Infrastructure.Persistence;

namespace Xenia.Tests.Email.Ingestion;

/// <summary>
/// Unit tests for durable sync lock behavior using an in-memory EF Core database.
///
/// Criteria verified:
/// - TryAcquireAsync succeeds when no lock exists
/// - TryAcquireAsync returns null when lock is already held
/// - Expired lock can be acquired by another instance
/// - IsLocked returns correct state
/// - DisposeAsync releases the lock (ExpiresAt set to past)
/// - Cross-source isolation: acquiring lock on source A does not block source B
/// - Cross-tenant isolation: same sourceId in different tenants does not conflict
/// - Double-dispose is safe (no exception)
/// </summary>
public sealed class DurableLockTests
{
    private static (DbEmailSourceSyncLock lockService, XeniaDbContext db) Create(
        TimeSpan? leaseDuration = null)
    {
        var services = new ServiceCollection();

        var dbName = $"lock_test_{Guid.NewGuid():N}";
        services.AddDbContext<XeniaDbContext>(o =>
            o.UseInMemoryDatabase(dbName));

        services.AddLogging();

        var provider = services.BuildServiceProvider();
        var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();
        var db = provider.GetRequiredService<XeniaDbContext>();
        db.Database.EnsureCreated();

        var opts = Options.Create(new XeniaIngestionOptions
        {
            SourceLockLeaseDuration = leaseDuration ?? TimeSpan.FromMinutes(10),
        });

        var logger = provider.GetRequiredService<ILogger<DbEmailSourceSyncLock>>();
        var lockService = new DbEmailSourceSyncLock(scopeFactory, opts, logger);
        return (lockService, db);
    }

    [Fact]
    public async Task TryAcquireAsync_NoExistingLock_ReturnsLease()
    {
        var (lockService, _) = Create();
        var tenantId  = Guid.NewGuid();
        var sourceId  = Guid.NewGuid();

        var lease = await lockService.TryAcquireAsync(tenantId, sourceId);

        Assert.NotNull(lease);
        await lease.DisposeAsync();
    }

    [Fact]
    public async Task TryAcquireAsync_AlreadyLocked_ReturnsNull()
    {
        var (lockService, _) = Create();
        var tenantId  = Guid.NewGuid();
        var sourceId  = Guid.NewGuid();

        var lease1 = await lockService.TryAcquireAsync(tenantId, sourceId);
        Assert.NotNull(lease1);

        var lease2 = await lockService.TryAcquireAsync(tenantId, sourceId);
        Assert.Null(lease2);

        await lease1.DisposeAsync();
    }

    [Fact]
    public async Task IsLocked_AfterAcquire_ReturnsTrue()
    {
        var (lockService, _) = Create();
        var tenantId  = Guid.NewGuid();
        var sourceId  = Guid.NewGuid();

        Assert.False(lockService.IsLocked(tenantId, sourceId));

        var lease = await lockService.TryAcquireAsync(tenantId, sourceId);
        Assert.NotNull(lease);
        Assert.True(lockService.IsLocked(tenantId, sourceId));

        await lease.DisposeAsync();
    }

    [Fact]
    public async Task IsLocked_AfterRelease_ReturnsFalse()
    {
        var (lockService, _) = Create();
        var tenantId  = Guid.NewGuid();
        var sourceId  = Guid.NewGuid();

        var lease = await lockService.TryAcquireAsync(tenantId, sourceId);
        Assert.NotNull(lease);
        await lease.DisposeAsync();

        Assert.False(lockService.IsLocked(tenantId, sourceId));
    }

    [Fact]
    public async Task TryAcquireAsync_AfterRelease_Succeeds()
    {
        var (lockService, _) = Create();
        var tenantId  = Guid.NewGuid();
        var sourceId  = Guid.NewGuid();

        var lease1 = await lockService.TryAcquireAsync(tenantId, sourceId);
        Assert.NotNull(lease1);
        await lease1.DisposeAsync();

        var lease2 = await lockService.TryAcquireAsync(tenantId, sourceId);
        Assert.NotNull(lease2);
        await lease2.DisposeAsync();
    }

    [Fact]
    public async Task CrossSourceIsolation_DifferentSourcesSameTenant_BothAcquire()
    {
        var (lockService, _) = Create();
        var tenantId  = Guid.NewGuid();
        var sourceA   = Guid.NewGuid();
        var sourceB   = Guid.NewGuid();

        var leaseA = await lockService.TryAcquireAsync(tenantId, sourceA);
        var leaseB = await lockService.TryAcquireAsync(tenantId, sourceB);

        Assert.NotNull(leaseA);
        Assert.NotNull(leaseB);

        await leaseA.DisposeAsync();
        await leaseB.DisposeAsync();
    }

    [Fact]
    public async Task CrossTenantIsolation_SameSourceIdDifferentTenants_BothAcquire()
    {
        var (lockService, _) = Create();
        var sourceId  = Guid.NewGuid();
        var tenantA   = Guid.NewGuid();
        var tenantB   = Guid.NewGuid();

        var leaseA = await lockService.TryAcquireAsync(tenantA, sourceId);
        var leaseB = await lockService.TryAcquireAsync(tenantB, sourceId);

        Assert.NotNull(leaseA);
        Assert.NotNull(leaseB);

        await leaseA.DisposeAsync();
        await leaseB.DisposeAsync();
    }

    [Fact]
    public async Task DoubleDispose_IsIdempotent()
    {
        var (lockService, _) = Create();
        var tenantId  = Guid.NewGuid();
        var sourceId  = Guid.NewGuid();

        var lease = await lockService.TryAcquireAsync(tenantId, sourceId);
        Assert.NotNull(lease);

        // Should not throw
        await lease.DisposeAsync();
        await lease.DisposeAsync();
    }

    [Fact]
    public async Task ExpiredLock_CanBeAcquiredByAnotherInstance()
    {
        // Use a very short lease duration so it expires immediately
        var (lockService, db) = Create(leaseDuration: TimeSpan.FromMilliseconds(1));
        var tenantId  = Guid.NewGuid();
        var sourceId  = Guid.NewGuid();

        var lease1 = await lockService.TryAcquireAsync(tenantId, sourceId);
        Assert.NotNull(lease1);

        // Wait for lease to expire
        await Task.Delay(50);

        // Another instance should now be able to acquire
        var lease2 = await lockService.TryAcquireAsync(tenantId, sourceId);
        Assert.NotNull(lease2);

        await lease2.DisposeAsync();
    }

    [Fact]
    public void DomainEntity_Create_HasCorrectState()
    {
        var tenantId  = Guid.NewGuid();
        var sourceId  = Guid.NewGuid();
        var leaseId   = "test-owner";
        var expiresAt = DateTime.UtcNow.AddMinutes(10);

        var lockRow = EmailSourceSyncLock.Create(tenantId, sourceId, leaseId, expiresAt);

        Assert.Equal(tenantId, lockRow.TenantId);
        Assert.Equal(sourceId, lockRow.EmailSourceId);
        Assert.Equal(leaseId, lockRow.LeaseOwnerId);
        Assert.Equal(expiresAt, lockRow.ExpiresAt);
        Assert.Equal(1, lockRow.Version);
        Assert.False(lockRow.IsExpired);
    }

    [Fact]
    public void DomainEntity_Release_SetsExpiredState()
    {
        var lockRow = EmailSourceSyncLock.Create(
            Guid.NewGuid(), Guid.NewGuid(), "owner",
            DateTime.UtcNow.AddMinutes(10));

        Assert.False(lockRow.IsExpired);
        lockRow.Release();
        Assert.True(lockRow.IsExpired);
    }

    [Fact]
    public void DomainEntity_Renew_WrongOwner_Throws()
    {
        var lockRow = EmailSourceSyncLock.Create(
            Guid.NewGuid(), Guid.NewGuid(), "owner-A",
            DateTime.UtcNow.AddMinutes(10));

        Assert.Throws<InvalidOperationException>(() =>
            lockRow.Renew("owner-B", DateTime.UtcNow.AddMinutes(20)));
    }
}
