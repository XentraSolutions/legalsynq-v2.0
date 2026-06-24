using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CareConnect.Application.Helpers;
using Xunit;

namespace CareConnect.Tests.Application;

public class AddressSelectionTokenHelperTests
{
    private const string Secret = "zip-token-test-secret";

    [Fact]
    public void Decode_ValidToken_ReturnsClaims()
    {
        var token = CreateToken(new AddressSelectionClaims("885", "Atlanta", "GA", "30316", 4_102_444_800));

        var claims = AddressSelectionTokenHelper.Decode(token, Secret, DateTimeOffset.FromUnixTimeSeconds(1_700_000_000));

        Assert.NotNull(claims);
        Assert.Equal("30316", claims!.PostalCode);
    }

    [Fact]
    public void Decode_CamelCaseToken_ReturnsClaims()
    {
        var token = CreateToken("""
            {"addressLine1":"885","city":"Atlanta","state":"GA","postalCode":"30316","exp":4102444800}
            """);

        var claims = AddressSelectionTokenHelper.Decode(token, Secret, DateTimeOffset.FromUnixTimeSeconds(1_700_000_000));

        Assert.NotNull(claims);
        Assert.Equal("885", claims!.AddressLine1);
        Assert.Equal("30316", claims.PostalCode);
    }

    [Fact]
    public void Decode_ExpiredToken_ReturnsNull()
    {
        var token = CreateToken(new AddressSelectionClaims("885", "Atlanta", "GA", "30316", 1));

        var claims = AddressSelectionTokenHelper.Decode(token, Secret, DateTimeOffset.FromUnixTimeSeconds(2));

        Assert.Null(claims);
    }

    [Fact]
    public void MatchesAddress_RejectsTamperedZip()
    {
        var claims = new AddressSelectionClaims("885", "Atlanta", "GA", "30316", 4_102_444_800);

        var matches = AddressSelectionTokenHelper.MatchesAddress(claims, "885", "Atlanta", "GA", "30317");

        Assert.False(matches);
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData("", true)]
    [InlineData("30316", true)]
    [InlineData("30316-1234", true)]
    [InlineData("3031", false)]
    [InlineData("30316-", false)]
    [InlineData("303161234", false)]
    public void IsValidOptionalUsZip_ValidatesExpectedFormats(string? zip, bool expected)
    {
        Assert.Equal(expected, PostalCodeHelper.IsValidOptionalUsZip(zip));
    }

    private static string CreateToken(AddressSelectionClaims claims)
    {
        var body = ToBase64Url(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(claims)));
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(Secret));
        var sig = ToBase64Url(hmac.ComputeHash(Encoding.UTF8.GetBytes(body)));
        return $"{body}.{sig}";
    }

    private static string CreateToken(string rawJson)
    {
        var body = ToBase64Url(Encoding.UTF8.GetBytes(rawJson));
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(Secret));
        var sig = ToBase64Url(hmac.ComputeHash(Encoding.UTF8.GetBytes(body)));
        return $"{body}.{sig}";
    }

    private static string ToBase64Url(byte[] bytes)
        => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
