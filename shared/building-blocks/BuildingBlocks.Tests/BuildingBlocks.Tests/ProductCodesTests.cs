using BuildingBlocks.Authorization;

namespace BuildingBlocks.Tests;

public class ProductCodesTests
{
    [Theory]
    [InlineData("XENIA")]
    [InlineData("Xenia")]
    [InlineData("SynqAI")]
    [InlineData("SYNQAI")]
    [InlineData("SYNQ_AI")]
    public void Normalize_MapsAllLegacyXeniaAliases_ToCanonicalCode(string rawCode)
    {
        Assert.Equal(ProductCodes.Xenia, ProductCodes.Normalize(rawCode));
    }

    [Theory]
    [InlineData("SynqLien", ProductCodes.SynqLiens)]
    [InlineData("SYNQ_LIEN", ProductCodes.SynqLiens)]
    [InlineData("CareConnect", ProductCodes.SynqCareConnect)]
    [InlineData("SynqFund", ProductCodes.SynqFund)]
    public void Normalize_MapsFrontendFriendlyAliases_ToCanonicalDbCodes(string rawCode, string expected)
    {
        Assert.Equal(expected, ProductCodes.Normalize(rawCode));
    }
}
