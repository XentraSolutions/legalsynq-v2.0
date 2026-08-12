using CareConnect.Domain;
using Xunit;

namespace CareConnect.Tests.Domain;

public class ReferralAttributionAccessCodeTests
{
    private static readonly Guid TenantId = Guid.CreateVersion7();
    private static readonly Guid AttributionId = Guid.CreateVersion7();

    private static ReferralAttributionAccessCode Make(DateTime? start = null, DateTime? end = null) =>
        ReferralAttributionAccessCode.Create(TenantId, AttributionId, "hash", start, end, createdByUserId: null);

    [Fact]
    public void Create_RejectsEmptyCodeHash()
    {
        Assert.Throws<ArgumentException>(() =>
            ReferralAttributionAccessCode.Create(TenantId, AttributionId, "", null, null, null));
    }

    [Fact]
    public void Create_RejectsEndBeforeStart()
    {
        var now = DateTime.UtcNow;
        Assert.Throws<ArgumentException>(() =>
            ReferralAttributionAccessCode.Create(
                TenantId, AttributionId, "hash", accessStartAtUtc: now, accessEndAtUtc: now.AddDays(-1), createdByUserId: null));
    }

    [Fact]
    public void Create_StartsActive()
    {
        var code = Make();
        Assert.True(code.IsActive);
    }

    [Fact]
    public void IsValidAt_ActiveCode_ActiveAttribution_NoDates_AlwaysValid()
    {
        var code = Make();
        Assert.True(code.IsValidAt(DateTime.UtcNow.AddYears(-5), attributionIsActive: true));
        Assert.True(code.IsValidAt(DateTime.UtcNow.AddYears(5), attributionIsActive: true));
    }

    [Fact]
    public void IsValidAt_InactiveCode_Denied()
    {
        var code = Make();
        code.SetActive(false, null);

        Assert.False(code.IsValidAt(DateTime.UtcNow, attributionIsActive: true));
    }

    /// <summary>
    /// Deactivating the parent attribution cuts off an otherwise-valid, in-window code's
    /// access immediately — distinct from the code's own IsActive flag. Callers must pass
    /// the attribution's current state; this entity never reaches into a (possibly-unloaded)
    /// navigation property to check it itself.
    /// </summary>
    [Fact]
    public void IsValidAt_ActiveCode_InactiveAttribution_Denied()
    {
        var code = Make();
        Assert.False(code.IsValidAt(DateTime.UtcNow, attributionIsActive: false));
    }

    [Fact]
    public void IsValidAt_WithinWindow_Valid()
    {
        var code = Make(start: DateTime.UtcNow.AddDays(-1), end: DateTime.UtcNow.AddDays(1));
        Assert.True(code.IsValidAt(DateTime.UtcNow, attributionIsActive: true));
    }

    [Fact]
    public void IsValidAt_BeforeStartDate_Denied()
    {
        var code = Make(start: DateTime.UtcNow.AddDays(3));
        Assert.False(code.IsValidAt(DateTime.UtcNow, attributionIsActive: true));
    }

    [Fact]
    public void IsValidAt_AfterEndDate_Denied()
    {
        var code = Make(end: DateTime.UtcNow.AddDays(1));
        Assert.False(code.IsValidAt(DateTime.UtcNow.AddDays(1).AddSeconds(1), attributionIsActive: true));
    }

    [Fact]
    public void SetActive_False_RevokesAccessImmediately()
    {
        var code = Make();
        Assert.True(code.IsValidAt(DateTime.UtcNow, attributionIsActive: true));

        code.SetActive(false, Guid.CreateVersion7());

        Assert.False(code.IsValidAt(DateTime.UtcNow, attributionIsActive: true));
    }
}
