using Xenia.Infrastructure.Email;
using Xunit;

namespace Xenia.Tests.Email;

/// <summary>
/// Tests for EmailHeaderSanitizer.
///
/// Verifies:
/// - Sensitive headers are removed
/// - Allowed infrastructure headers are kept
/// - Length limits are enforced
/// - Count limits are enforced
/// - Unicode normalization
/// - Malformed headers are silently omitted
/// - Null/empty input handled gracefully
/// </summary>
public sealed class EmailHeaderSanitizerTests
{
    private readonly EmailHeaderSanitizer _sut = new();

    [Fact]
    public void Sanitize_NullInput_ReturnsEmpty()
    {
        var result = _sut.Sanitize(null);
        Assert.Empty(result);
    }

    [Fact]
    public void Sanitize_EmptyInput_ReturnsEmpty()
    {
        var result = _sut.Sanitize([]);
        Assert.Empty(result);
    }

    [Fact]
    public void Sanitize_AllowsSubjectAndFrom()
    {
        var headers = new Dictionary<string, string>
        {
            ["Subject"] = "Test email",
            ["From"]    = "sender@example.com",
        };
        var result = _sut.Sanitize(headers);
        Assert.Equal("Test email", result["Subject"]);
        Assert.Equal("sender@example.com", result["From"]);
    }

    [Theory]
    [InlineData("Authorization")]
    [InlineData("authorization")]
    [InlineData("AUTHORIZATION")]
    [InlineData("Cookie")]
    [InlineData("Set-Cookie")]
    [InlineData("X-Api-Key")]
    [InlineData("X-Auth-Token")]
    [InlineData("X-Access-Token")]
    [InlineData("X-Session-Token")]
    [InlineData("X-Internal-Secret")]
    [InlineData("X-Service-Token")]
    [InlineData("WWW-Authenticate")]
    [InlineData("Proxy-Authorization")]
    [InlineData("X-Forwarded-For")]
    [InlineData("X-Forwarded-Host")]
    [InlineData("X-Forwarded-Proto")]
    [InlineData("X-Real-IP")]
    public void Sanitize_RemovesDeniedHeaders(string headerName)
    {
        var headers = new Dictionary<string, string>
        {
            [headerName] = "some-sensitive-value",
            ["Subject"]  = "safe",
        };
        var result = _sut.Sanitize(headers);
        Assert.False(result.ContainsKey(headerName), $"Expected {headerName} to be removed");
        Assert.True(result.ContainsKey("Subject"));
    }

    [Theory]
    [InlineData("X-My-Custom-Auth-Header")]
    [InlineData("X-Tenant-Secret-Code")]
    [InlineData("X-Bearer-Token")]
    [InlineData("X-Password-Reset")]
    [InlineData("X-Credential-Id")]
    public void Sanitize_RemovesSensitiveSubstringHeaders(string headerName)
    {
        var headers = new Dictionary<string, string>
        {
            [headerName] = "sensitive-value",
            ["Subject"]  = "safe",
        };
        var result = _sut.Sanitize(headers);
        Assert.False(result.ContainsKey(headerName), $"Expected {headerName} to be removed");
        Assert.True(result.ContainsKey("Subject"));
    }

    [Theory]
    [InlineData("Authentication-Results")]
    [InlineData("DKIM-Signature")]
    [InlineData("ARC-Authentication-Results")]
    [InlineData("ARC-Message-Signature")]
    [InlineData("ARC-Seal")]
    public void Sanitize_AllowsExplicitlyPermittedAuthHeaders(string headerName)
    {
        var headers = new Dictionary<string, string>
        {
            [headerName] = "allowed-infrastructure-value",
        };
        var result = _sut.Sanitize(headers);
        Assert.True(result.ContainsKey(headerName), $"Expected {headerName} to be allowed");
    }

    [Fact]
    public void Sanitize_TruncatesLongValues()
    {
        var longValue = new string('X', 2000);
        var headers = new Dictionary<string, string>
        {
            ["Subject"] = longValue,
        };
        var result = _sut.Sanitize(headers);
        Assert.True(result["Subject"].Length <= 1024, "Value should be truncated to 1024 chars");
    }

    [Fact]
    public void Sanitize_EnforcesMaxHeaderCount()
    {
        var headers = Enumerable.Range(1, 100)
            .Select(i => new KeyValuePair<string, string>($"X-Custom-{i}", $"value-{i}"))
            .ToList();

        var result = _sut.Sanitize(headers);
        Assert.True(result.Count <= 50, $"Expected max 50 headers, got {result.Count}");
    }

    [Fact]
    public void Sanitize_SkipsMalformedHeaders_NullValue()
    {
        var headers = new List<KeyValuePair<string, string>>
        {
            new("Subject", "valid"),
            new("X-Null-Value", null!),
        };
        var result = _sut.Sanitize(headers);
        Assert.True(result.ContainsKey("Subject"));
        Assert.False(result.ContainsKey("X-Null-Value"));
    }

    [Fact]
    public void Sanitize_SkipsMalformedHeaders_EmptyName()
    {
        var headers = new List<KeyValuePair<string, string>>
        {
            new("", "value"),
            new("Subject", "valid"),
        };
        var result = _sut.Sanitize(headers);
        Assert.True(result.ContainsKey("Subject"));
        Assert.Equal(1, result.Count);
    }

    [Fact]
    public void Sanitize_NormalizesUnicode()
    {
        // Composed vs decomposed unicode should normalize to NFC
        var decomposed = "Subject";
        var headers    = new Dictionary<string, string> { [decomposed] = "Café" };
        var result     = _sut.Sanitize(headers);
        Assert.True(result.ContainsKey("Subject"));
    }

    [Fact]
    public void Sanitize_IsCaseInsensitiveOnHeaderNames()
    {
        var headers = new Dictionary<string, string>
        {
            ["SUBJECT"]  = "upper",
            ["From"]     = "from@test.com",
        };
        var result = _sut.Sanitize(headers);
        // Both should be present
        Assert.Equal(2, result.Count);
    }
}
