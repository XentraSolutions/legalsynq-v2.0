using Identity.Domain;
using Xunit;

namespace Identity.Tests;

public class ProductEligibilityConfigTests
{
    [Theory]
    [InlineData(OrgType.LawFirm)]
    [InlineData(OrgType.LienOwner)]
    [InlineData(OrgType.Internal)]
    public void SynqSelling_IsAvailableToSellingOrganizations(string organizationType)
    {
        Assert.True(ProductEligibilityConfig.IsEligible(
            organizationType,
            ProductCodes.SynqSelling));
    }

    [Theory]
    [InlineData(OrgType.Provider)]
    [InlineData(OrgType.Funder)]
    public void SynqSelling_IsNotAutomaticallyProvisionedToOtherOrganizations(string organizationType)
    {
        Assert.False(ProductEligibilityConfig.IsEligible(
            organizationType,
            ProductCodes.SynqSelling));
    }
}
