using CareConnect.Application.Helpers;
using Xunit;

namespace CareConnect.Tests.Application;

public class PhoneNumberHelperTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void TryNormalizeOptionalUsPhone_AllowsBlankValues(string? phone)
    {
        var ok = PhoneNumberHelper.TryNormalizeOptionalUsPhone(phone, out var normalized);

        Assert.True(ok);
        Assert.Null(normalized);
    }

    [Theory]
    [InlineData("7025551234", "7025551234")]
    [InlineData("(702) 555-1234", "7025551234")]
    [InlineData("702-555-1234", "7025551234")]
    public void TryNormalizeOptionalUsPhone_NormalizesTenDigitPhones(string phone, string expected)
    {
        var ok = PhoneNumberHelper.TryNormalizeOptionalUsPhone(phone, out var normalized);

        Assert.True(ok);
        Assert.Equal(expected, normalized);
    }

    [Theory]
    [InlineData("123")]
    [InlineData("555123456")]
    [InlineData("55512345678")]
    public void TryNormalizeOptionalUsPhone_RejectsWrongLength(string phone)
    {
        var ok = PhoneNumberHelper.TryNormalizeOptionalUsPhone(phone, out var normalized);

        Assert.False(ok);
        Assert.Null(normalized);
    }
}
