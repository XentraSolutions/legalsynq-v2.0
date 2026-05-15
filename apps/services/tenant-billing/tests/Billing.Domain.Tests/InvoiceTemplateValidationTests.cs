using Billing.Domain.Services;
using Xunit;

namespace Billing.Domain.Tests;

public class InvoiceTemplateValidationTests
{
    // -----------------------------------------------------------------
    // Name
    // -----------------------------------------------------------------

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void NormalizeName_RejectsEmpty(string? value)
    {
        Assert.Throws<ArgumentException>(() => InvoiceTemplateValidation.NormalizeName(value));
    }

    [Fact]
    public void NormalizeName_TrimsWhitespace()
    {
        Assert.Equal("Default", InvoiceTemplateValidation.NormalizeName("  Default  "));
    }

    [Fact]
    public void NormalizeName_RejectsOverLongValues()
    {
        var tooLong = new string('a', InvoiceTemplateValidation.NameMaxLength + 1);
        Assert.Throws<ArgumentException>(() => InvoiceTemplateValidation.NormalizeName(tooLong));
    }

    // -----------------------------------------------------------------
    // Accent color
    // -----------------------------------------------------------------

    [Theory]
    [InlineData("#1f4fff", "#1F4FFF")]
    [InlineData("  #abcdef  ", "#ABCDEF")]
    public void NormalizeAccentColor_NormalizesHex(string input, string expected)
    {
        Assert.Equal(expected, InvoiceTemplateValidation.NormalizeAccentColor(input));
    }

    [Theory]
    [InlineData("#FFF")] // shorthand not allowed
    [InlineData("FFFFFF")] // missing #
    [InlineData("#GGGGGG")] // not hex
    [InlineData("blue")]
    public void NormalizeAccentColor_RejectsInvalid(string input)
    {
        Assert.Throws<ArgumentException>(() => InvoiceTemplateValidation.NormalizeAccentColor(input));
    }

    [Fact]
    public void NormalizeAccentColor_NullPassesThrough()
    {
        Assert.Null(InvoiceTemplateValidation.NormalizeAccentColor(null));
        Assert.Null(InvoiceTemplateValidation.NormalizeAccentColor("   "));
    }

    // -----------------------------------------------------------------
    // LogoUrl
    // -----------------------------------------------------------------

    [Theory]
    [InlineData("https://example.com/logo.png")]
    [InlineData("HTTP://EXAMPLE.COM/x.png")]
    [InlineData("/uploads/logo.png")]
    [InlineData("/a/b-c_d/e.svg")]
    public void NormalizeLogoUrl_AcceptsValid(string input)
    {
        Assert.Equal(input.Trim(), InvoiceTemplateValidation.NormalizeLogoUrl(input));
    }

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("ftp://example.com/x.png")]
    [InlineData("data:image/png;base64,abc")]
    [InlineData("relative/path.png")]
    [InlineData("/has spaces.png")]
    public void NormalizeLogoUrl_RejectsInvalid(string input)
    {
        Assert.Throws<ArgumentException>(() => InvoiceTemplateValidation.NormalizeLogoUrl(input));
    }

    // -----------------------------------------------------------------
    // Invoice number prefix
    // -----------------------------------------------------------------

    [Theory]
    [InlineData("inv", "INV")]
    [InlineData("INV-2026", "INV-2026")]
    [InlineData("a", "A")]
    public void NormalizeInvoiceNumberPrefix_NormalizesAndValidates(string input, string expected)
    {
        Assert.Equal(expected, InvoiceTemplateValidation.NormalizeInvoiceNumberPrefix(input));
    }

    [Theory]
    [InlineData("INV_2026")] // underscore not allowed
    [InlineData("INV 2026")] // space
    [InlineData("INV.2026")] // dot
    public void NormalizeInvoiceNumberPrefix_RejectsInvalidCharacters(string input)
    {
        Assert.Throws<ArgumentException>(() => InvoiceTemplateValidation.NormalizeInvoiceNumberPrefix(input));
    }

    [Fact]
    public void NormalizeInvoiceNumberPrefix_RejectsOverLong()
    {
        var tooLong = new string('A', InvoiceTemplateValidation.InvoiceNumberPrefixMaxLength + 1);
        Assert.Throws<ArgumentException>(() => InvoiceTemplateValidation.NormalizeInvoiceNumberPrefix(tooLong));
    }

    // -----------------------------------------------------------------
    // DefaultDueDays
    // -----------------------------------------------------------------

    [Theory]
    [InlineData(0)]
    [InlineData(30)]
    [InlineData(365)]
    public void ValidateDefaultDueDays_AcceptsInRange(int value)
    {
        Assert.Equal(value, InvoiceTemplateValidation.ValidateDefaultDueDays(value));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(366)]
    public void ValidateDefaultDueDays_RejectsOutOfRange(int value)
    {
        Assert.Throws<ArgumentException>(() => InvoiceTemplateValidation.ValidateDefaultDueDays(value));
    }

    [Fact]
    public void ValidateDefaultDueDays_NullPassesThrough()
    {
        Assert.Null(InvoiceTemplateValidation.ValidateDefaultDueDays(null));
    }

    // -----------------------------------------------------------------
    // Status / OwnerType
    // -----------------------------------------------------------------

    [Theory]
    [InlineData("Draft")]
    [InlineData("Active")]
    [InlineData("Retired")]
    public void ValidateStatus_AcceptsKnown(string status)
    {
        Assert.Equal(status, InvoiceTemplateValidation.ValidateStatus(status));
    }

    [Fact]
    public void ValidateStatus_RejectsUnknown()
    {
        Assert.Throws<ArgumentException>(() => InvoiceTemplateValidation.ValidateStatus("Archived"));
    }

    [Theory]
    [InlineData("Platform")]
    [InlineData("Tenant")]
    public void ValidateOwnerType_AcceptsKnown(string owner)
    {
        Assert.Equal(owner, InvoiceTemplateValidation.ValidateOwnerType(owner));
    }

    [Fact]
    public void ValidateOwnerType_RejectsUnknown()
    {
        Assert.Throws<ArgumentException>(() => InvoiceTemplateValidation.ValidateOwnerType("Reseller"));
    }

    // -----------------------------------------------------------------
    // INV-TPL-04: issuer fields
    // -----------------------------------------------------------------

    [Theory]
    [InlineData("ar@brand.test", "ar@brand.test")]
    [InlineData("  AR@Brand.Test  ", "ar@brand.test")]
    public void NormalizeIssuerEmail_TrimsAndLowercases(string input, string expected)
    {
        Assert.Equal(expected, InvoiceTemplateValidation.NormalizeIssuerEmail(input));
    }

    [Fact]
    public void NormalizeIssuerEmail_NullOrBlank_ReturnsNull()
    {
        Assert.Null(InvoiceTemplateValidation.NormalizeIssuerEmail(null));
        Assert.Null(InvoiceTemplateValidation.NormalizeIssuerEmail("   "));
    }

    [Theory]
    [InlineData("not-an-email")]
    [InlineData("missing-at.example.com")]
    [InlineData("two@@signs.test")]
    [InlineData("user@no-tld")]
    public void NormalizeIssuerEmail_RejectsBadShape(string input)
    {
        Assert.Throws<ArgumentException>(
            () => InvoiceTemplateValidation.NormalizeIssuerEmail(input));
    }

    [Fact]
    public void NormalizeIssuerEmail_RejectsOverLong()
    {
        var tooLong = new string('a', InvoiceTemplateValidation.IssuerEmailMaxLength + 1);
        Assert.Throws<ArgumentException>(
            () => InvoiceTemplateValidation.NormalizeIssuerEmail(tooLong + "@x.test"));
    }

    [Theory]
    [InlineData("https://brand.test")]
    [InlineData("http://brand.test/path?x=1")]
    [InlineData("  https://brand.test  ")]
    public void NormalizeIssuerWebsite_AcceptsAbsoluteHttpUrl(string input)
    {
        var result = InvoiceTemplateValidation.NormalizeIssuerWebsite(input);
        Assert.NotNull(result);
        Assert.StartsWith("http", result);
    }

    [Fact]
    public void NormalizeIssuerWebsite_NullOrBlank_ReturnsNull()
    {
        Assert.Null(InvoiceTemplateValidation.NormalizeIssuerWebsite(null));
        Assert.Null(InvoiceTemplateValidation.NormalizeIssuerWebsite("   "));
    }

    [Theory]
    [InlineData("brand.test")]               // missing scheme
    [InlineData("/relative/path")]           // relative
    [InlineData("ftp://brand.test")]         // wrong scheme
    [InlineData("javascript:alert('xss')")]  // hostile scheme
    public void NormalizeIssuerWebsite_RejectsNonHttp(string input)
    {
        Assert.Throws<ArgumentException>(
            () => InvoiceTemplateValidation.NormalizeIssuerWebsite(input));
    }

    [Fact]
    public void NormalizeIssuerWebsite_RejectsOverLong()
    {
        var pad = new string('a',
            InvoiceTemplateValidation.IssuerWebsiteMaxLength + 1);
        Assert.Throws<ArgumentException>(
            () => InvoiceTemplateValidation.NormalizeIssuerWebsite("https://" + pad));
    }

    // The 9 remaining issuer fields use NormalizeOptionalText with their
    // max-length constants and are exercised through the service / DTO
    // layer; we cover the constants explicitly here as a safety net so
    // accidental schema changes break the test, not the migration.

    [Fact]
    public void IssuerMaxLengthConstants_MatchTemplateColumnBudget()
    {
        Assert.Equal(200, InvoiceTemplateValidation.IssuerDisplayNameMaxLength);
        Assert.Equal(250, InvoiceTemplateValidation.IssuerLegalNameMaxLength);
        Assert.Equal(250, InvoiceTemplateValidation.IssuerAddressLineMaxLength);
        Assert.Equal(100, InvoiceTemplateValidation.IssuerCityMaxLength);
        Assert.Equal(100, InvoiceTemplateValidation.IssuerStateRegionMaxLength);
        Assert.Equal(100, InvoiceTemplateValidation.IssuerPostalCodeMaxLength);
        Assert.Equal(100, InvoiceTemplateValidation.IssuerCountryMaxLength);
        Assert.Equal(320, InvoiceTemplateValidation.IssuerEmailMaxLength);
        Assert.Equal(50, InvoiceTemplateValidation.IssuerPhoneMaxLength);
        Assert.Equal(100, InvoiceTemplateValidation.IssuerTaxIdMaxLength);
        Assert.Equal(500, InvoiceTemplateValidation.IssuerWebsiteMaxLength);
    }
}
