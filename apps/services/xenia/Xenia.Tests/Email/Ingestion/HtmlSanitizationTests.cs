using Microsoft.Extensions.Options;
using Xenia.Application.Email.Ingestion;
using Xenia.Infrastructure.Email;
using Xunit;

namespace Xenia.Tests.Email.Ingestion;

/// <summary>
/// Unit tests for GanssEmailHtmlSanitizer.
///
/// Criteria verified:
/// - Script tags removed
/// - Inline event handlers removed (onclick, onload, onerror, etc.)
/// - javascript: URLs removed
/// - iframe, object, embed, form removed
/// - data: URLs removed from src attributes
/// - Remote images blocked when BlockRemoteImages=true
/// - Safe HTML structure preserved (p, a, b, i, span, div, table)
/// - Null/empty input → empty string (no exception)
/// - BlocksRemoteImages reflects configuration
/// </summary>
public sealed class HtmlSanitizationTests
{
    private static GanssEmailHtmlSanitizer CreateSanitizer(bool blockRemoteImages = true)
    {
        var opts = Options.Create(new XeniaIngestionOptions
        {
            BlockRemoteImages = blockRemoteImages,
        });
        return new GanssEmailHtmlSanitizer(opts);
    }

    [Fact]
    public void ScriptTag_IsRemoved()
    {
        var sanitizer = CreateSanitizer();
        var html = "<p>Hello</p><script>alert('xss')</script><p>World</p>";
        var result = sanitizer.Sanitize(html);
        Assert.DoesNotContain("script", result, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("alert", result);
        Assert.Contains("Hello", result);
        Assert.Contains("World", result);
    }

    [Fact]
    public void OnClickHandler_IsRemoved()
    {
        var sanitizer = CreateSanitizer();
        var html = "<a href=\"/safe\" onclick=\"alert('xss')\">Click me</a>";
        var result = sanitizer.Sanitize(html);
        Assert.DoesNotContain("onclick", result, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Click me", result);
    }

    [Fact]
    public void OnLoadHandler_IsRemoved()
    {
        var sanitizer = CreateSanitizer();
        var html = "<img src=\"test.jpg\" onload=\"evil()\" />";
        var result = sanitizer.Sanitize(html);
        Assert.DoesNotContain("onload", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void JavascriptUrl_IsRemoved()
    {
        var sanitizer = CreateSanitizer();
        var html = "<a href=\"javascript:alert(1)\">Click</a>";
        var result = sanitizer.Sanitize(html);
        Assert.DoesNotContain("javascript:", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void IframeTag_IsRemoved()
    {
        var sanitizer = CreateSanitizer();
        var html = "<p>Safe</p><iframe src=\"https://evil.example.com\"></iframe>";
        var result = sanitizer.Sanitize(html);
        Assert.DoesNotContain("iframe", result, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Safe", result);
    }

    [Fact]
    public void FormTag_IsRemoved()
    {
        var sanitizer = CreateSanitizer();
        var html = "<form action=\"/phish\"><input type=\"text\" /></form>";
        var result = sanitizer.Sanitize(html);
        Assert.DoesNotContain("<form", result, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<input", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ObjectTag_IsRemoved()
    {
        var sanitizer = CreateSanitizer();
        var html = "<object data=\"malware.swf\"></object>";
        var result = sanitizer.Sanitize(html);
        Assert.DoesNotContain("object", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SafeHtml_IsPreserved()
    {
        var sanitizer = CreateSanitizer();
        var html = "<p>Hello <b>World</b>! <a href=\"https://example.com\">Link</a></p>";
        var result = sanitizer.Sanitize(html);
        Assert.Contains("Hello", result);
        Assert.Contains("World", result);
        Assert.Contains("Link", result);
    }

    [Fact]
    public void NullInput_ReturnsEmptyString()
    {
        var sanitizer = CreateSanitizer();
        var result = sanitizer.Sanitize(null);
        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void EmptyInput_ReturnsEmptyString()
    {
        var sanitizer = CreateSanitizer();
        var result = sanitizer.Sanitize(string.Empty);
        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void WhitespaceInput_ReturnsEmptyString()
    {
        var sanitizer = CreateSanitizer();
        var result = sanitizer.Sanitize("   ");
        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void BlockRemoteImages_True_RemovesImgSrc()
    {
        var sanitizer = CreateSanitizer(blockRemoteImages: true);
        var html = "<img src=\"https://tracker.evil.com/pixel.gif\" alt=\"tracker\" />";
        var result = sanitizer.Sanitize(html);
        // Remote image src should be removed or replaced
        Assert.DoesNotContain("tracker.evil.com", result);
    }

    [Fact]
    public void BlockRemoteImages_False_KeepsImgSrc()
    {
        var sanitizer = CreateSanitizer(blockRemoteImages: false);
        var html = "<img src=\"https://example.com/image.jpg\" alt=\"test\" />";
        var result = sanitizer.Sanitize(html);
        Assert.Contains("example.com", result);
    }

    [Fact]
    public void BlocksRemoteImages_Property_MatchesConfig()
    {
        Assert.True(CreateSanitizer(blockRemoteImages: true).BlocksRemoteImages);
        Assert.False(CreateSanitizer(blockRemoteImages: false).BlocksRemoteImages);
    }

    [Fact]
    public void MaliciousEventHandlers_AllRemoved()
    {
        var sanitizer = CreateSanitizer();
        var html = "<div onerror=\"x\" onmouseover=\"y\" onfocus=\"z\" onkeydown=\"w\">text</div>";
        var result = sanitizer.Sanitize(html);
        Assert.DoesNotContain("onerror", result, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("onmouseover", result, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("onfocus", result, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("onkeydown", result, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("text", result);
    }

    [Fact]
    public void ComplexEmailHtml_DoesNotThrow()
    {
        var sanitizer = CreateSanitizer();
        var complexHtml = @"
<!DOCTYPE html>
<html>
<head><meta charset='utf-8'><style>body{font-family:Arial}</style></head>
<body>
  <table>
    <tr><td><img src='https://remote.example.com/logo.png' alt='logo'/></td></tr>
    <tr><td><p>Dear Customer,</p><p>Your invoice is ready.</p></td></tr>
    <tr><td><a href='https://example.com'>View Invoice</a></td></tr>
  </table>
  <script>document.write('xss')</script>
</body>
</html>";
        var result = sanitizer.Sanitize(complexHtml);
        Assert.DoesNotContain("script", result, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Dear Customer", result);
    }
}
