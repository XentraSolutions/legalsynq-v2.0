using Xenia.Domain.Email;
using Xunit;

namespace Xenia.Tests.Email;

/// <summary>
/// Tests for EmailSourceSyncLock fencing token and lease renewal failure tracking.
///
/// Verifies:
/// - FencingToken increments on Acquire()
/// - FencingToken does not change on Renew()
/// - ValidateFencingToken returns correct results
/// - RecordRenewalFailure increments count and returns threshold-reached signal
/// - RenewalFailureCount resets on Renew()
/// - RenewalFailureCount resets on Acquire()
/// </summary>
public sealed class EmailSourceSyncLockFencingTests
{
    private static EmailSourceSyncLock CreateLock()
        => EmailSourceSyncLock.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "worker-1",
            DateTime.UtcNow.AddMinutes(5));

    [Fact]
    public void Create_SetsFencingTokenToOne()
    {
        var lk = CreateLock();
        Assert.Equal(1L, lk.FencingToken);
    }

    [Fact]
    public void Create_SetsRenewalFailureCountToZero()
    {
        var lk = CreateLock();
        Assert.Equal(0, lk.RenewalFailureCount);
    }

    [Fact]
    public void Acquire_IncrementsFencingToken()
    {
        var lk = CreateLock();
        Assert.Equal(1L, lk.FencingToken);
        lk.Acquire("worker-2", DateTime.UtcNow.AddMinutes(5));
        Assert.Equal(2L, lk.FencingToken);
        lk.Acquire("worker-3", DateTime.UtcNow.AddMinutes(5));
        Assert.Equal(3L, lk.FencingToken);
    }

    [Fact]
    public void Acquire_ResetsRenewalFailureCount()
    {
        var lk = CreateLock();
        lk.RecordRenewalFailure();
        lk.RecordRenewalFailure();
        Assert.Equal(2, lk.RenewalFailureCount);
        lk.Acquire("worker-2", DateTime.UtcNow.AddMinutes(5));
        Assert.Equal(0, lk.RenewalFailureCount);
    }

    [Fact]
    public void Renew_DoesNotChangeFencingToken()
    {
        var lk    = CreateLock();
        var token = lk.FencingToken;
        lk.Renew("worker-1", DateTime.UtcNow.AddMinutes(10));
        Assert.Equal(token, lk.FencingToken);
    }

    [Fact]
    public void Renew_ResetsRenewalFailureCount()
    {
        var lk = CreateLock();
        lk.RecordRenewalFailure();
        Assert.Equal(1, lk.RenewalFailureCount);
        lk.Renew("worker-1", DateTime.UtcNow.AddMinutes(10));
        Assert.Equal(0, lk.RenewalFailureCount);
    }

    [Fact]
    public void Renew_ThrowsOnWrongOwner()
    {
        var lk = CreateLock();
        Assert.Throws<InvalidOperationException>(() =>
            lk.Renew("wrong-worker", DateTime.UtcNow.AddMinutes(10)));
    }

    [Fact]
    public void ValidateFencingToken_ReturnsTrueForCurrentToken()
    {
        var lk = CreateLock();
        Assert.True(lk.ValidateFencingToken(1L));
    }

    [Fact]
    public void ValidateFencingToken_ReturnsFalseForStaleToken()
    {
        var lk = CreateLock();
        lk.Acquire("worker-2", DateTime.UtcNow.AddMinutes(5));
        Assert.False(lk.ValidateFencingToken(1L)); // stale token
        Assert.True(lk.ValidateFencingToken(2L));  // current token
    }

    [Fact]
    public void RecordRenewalFailure_IncrementsCount()
    {
        var lk = CreateLock();
        var exceeded1 = lk.RecordRenewalFailure(failureThreshold: 3);
        Assert.False(exceeded1);
        Assert.Equal(1, lk.RenewalFailureCount);

        lk.RecordRenewalFailure(failureThreshold: 3);
        var exceeded3 = lk.RecordRenewalFailure(failureThreshold: 3);
        Assert.True(exceeded3);
        Assert.Equal(3, lk.RenewalFailureCount);
    }

    [Fact]
    public void RecordRenewalFailure_DefaultThreshold_IsThree()
    {
        var lk = CreateLock();
        Assert.False(lk.RecordRenewalFailure());
        Assert.False(lk.RecordRenewalFailure());
        Assert.True(lk.RecordRenewalFailure());
    }
}
