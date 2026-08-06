using CareConnect.Domain;
using Xunit;

namespace CareConnect.Tests.Domain;

public class ReferralAttributionTests
{
    private static readonly Guid TenantId = Guid.CreateVersion7();

    [Fact]
    public void Create_NormalizesCodeToUppercaseTrimmed()
    {
        var a = ReferralAttribution.Create(TenantId, "Cam", "Perry", "  cam_perry  ", null, true, 1, null);

        Assert.Equal("CAM_PERRY", a.Code);
        Assert.Equal("Cam", a.FirstName);
        Assert.Equal("Perry", a.LastName);
        Assert.Equal("Cam Perry", a.FullName);
        Assert.Equal(TenantId, a.TenantId);
        Assert.True(a.IsActive);
        Assert.Equal(1, a.DisplayOrder);
    }

    [Fact]
    public void Create_EmptyFirstName_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            ReferralAttribution.Create(TenantId, "  ", "Perry", "CAM_PERRY", null, true, null, null));
    }

    [Fact]
    public void Create_EmptyLastName_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            ReferralAttribution.Create(TenantId, "Cam", "  ", "CAM_PERRY", null, true, null, null));
    }

    [Fact]
    public void Create_EmptyCode_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            ReferralAttribution.Create(TenantId, "Cam", "Perry", "   ", null, true, null, null));
    }

    [Fact]
    public void Update_ChangesDisplayFieldsButNeverCodeOrTenant()
    {
        var a = ReferralAttribution.Create(TenantId, "Cam", "Perry", "CAM_PERRY", null, true, 1, null);
        var originalCode = a.Code;
        var originalTenant = a.TenantId;

        a.Update("Camden", "Perry", "Updated description", 2, Guid.CreateVersion7());

        Assert.Equal("Camden", a.FirstName);
        Assert.Equal("Perry", a.LastName);
        Assert.Equal("Updated description", a.Description);
        Assert.Equal(2, a.DisplayOrder);
        Assert.Equal(originalCode, a.Code);
        Assert.Equal(originalTenant, a.TenantId);
    }

    [Fact]
    public void SetActive_TogglesIsActiveAndTracksUpdatedBy()
    {
        var a = ReferralAttribution.Create(TenantId, "Cam", "Perry", "CAM_PERRY", null, true, null, null);
        var actorId = Guid.CreateVersion7();

        a.SetActive(false, actorId);

        Assert.False(a.IsActive);
        Assert.Equal(actorId, a.UpdatedByUserId);
    }
}
