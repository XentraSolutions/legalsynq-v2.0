using Microsoft.Extensions.Logging.Abstractions;
using Xenia.Infrastructure.Security;
using Xunit;

namespace Xenia.Tests.Security;

/// <summary>
/// Phase E — validates that XeniaSensitiveDataGuard correctly identifies
/// and redacts sensitive data patterns before they reach audit detail fields or logs.
/// </summary>
public sealed class XeniaSensitiveDataGuardTests
{
    // ── ContainsSensitiveData ────────────────────────────────────────────────

    [Theory]
    [InlineData("password=hunter2")]
    [InlineData("secret=abc123def456ghi")]
    [InlineData("token=eyJhbGciOiJIUzI1NiJ9.payload.sig")]
    [InlineData("bearer=some-long-bearer-token-value")]
    [InlineData("api_key=sk-1234567890abcdef")]
    [InlineData("client_secret=very-secret-value-here")]
    [InlineData("body_text=This is a very long email body that definitely should not be stored in audit")]
    [InlineData("cursor=ABCDEFGHIJ0123456789ABCDEFGHIJ==")]
    public void ContainsSensitiveData_ReturnsTrueForSensitiveInput(string input)
    {
        Assert.True(XeniaSensitiveDataGuard.ContainsSensitiveData(input));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Sync run completed. Messages: 12, Pages: 3")]
    [InlineData("Source health changed. Status: Healthy")]
    [InlineData("Attachment dispatched. Extension: .pdf")]
    [InlineData("Duplicate detected. MessageId: abc-123")]
    [InlineData("Alert resolved. AlertId: 550e8400-e29b-41d4-a716-446655440000")]
    public void ContainsSensitiveData_ReturnsFalseForSafeInput(string? input)
    {
        Assert.False(XeniaSensitiveDataGuard.ContainsSensitiveData(input));
    }

    // ── SanitizeAttachmentFilename ───────────────────────────────────────────

    [Theory]
    [InlineData("invoice-2026-01-15.pdf", ".pdf")]
    [InlineData("report.docx", ".docx")]
    [InlineData("image.PNG", ".png")]
    [InlineData("archive.tar.gz", ".gz")]
    public void SanitizeAttachmentFilename_ReturnsOnlyExtension(string filename, string expectedExt)
    {
        var result = XeniaSensitiveDataGuard.SanitizeAttachmentFilename(filename);
        Assert.Equal(expectedExt, result);
    }

    [Theory]
    [InlineData(null, "[no-filename]")]
    [InlineData("", "[no-filename]")]
    [InlineData("filewithnoextension", "[no-ext]")]
    public void SanitizeAttachmentFilename_HandlesEdgeCases(string? filename, string expected)
    {
        Assert.Equal(expected, XeniaSensitiveDataGuard.SanitizeAttachmentFilename(filename));
    }

    // ── IsHighRiskMimeType ───────────────────────────────────────────────────

    [Theory]
    [InlineData("application/x-msdownload")]
    [InlineData("application/octet-stream")]
    [InlineData("application/x-executable")]
    [InlineData("application/x-dosexec")]
    public void IsHighRiskMimeType_ReturnsTrueForDangerousMimes(string mime)
    {
        Assert.True(XeniaSensitiveDataGuard.IsHighRiskMimeType(mime));
    }

    [Theory]
    [InlineData("application/pdf")]
    [InlineData("image/png")]
    [InlineData("text/plain")]
    [InlineData("application/vnd.openxmlformats-officedocument.wordprocessingml.document")]
    [InlineData(null)]
    [InlineData("")]
    public void IsHighRiskMimeType_ReturnsFalseForSafeMimes(string? mime)
    {
        Assert.False(XeniaSensitiveDataGuard.IsHighRiskMimeType(mime));
    }

    // ── IsDangerousFilename ──────────────────────────────────────────────────

    [Theory]
    [InlineData("exploit.exe")]
    [InlineData("dropper.bat")]
    [InlineData("script.cmd")]
    [InlineData("macro.vbs")]
    [InlineData("installer.msi")]
    [InlineData("payload.dll")]
    public void IsDangerousFilename_ReturnsTrueForHighRiskExtensions(string filename)
    {
        Assert.True(XeniaSensitiveDataGuard.IsDangerousFilename(filename));
    }

    [Theory]
    [InlineData("document.pdf")]
    [InlineData("photo.jpg")]
    [InlineData("spreadsheet.xlsx")]
    [InlineData(null)]
    [InlineData("")]
    public void IsDangerousFilename_ReturnsFalseForSafeFiles(string? filename)
    {
        Assert.False(XeniaSensitiveDataGuard.IsDangerousFilename(filename));
    }

    // ── ValidateAndSanitizeAuditDetail ───────────────────────────────────────

    [Fact]
    public void ValidateAndSanitizeAuditDetail_PassesThroughSafeDetail()
    {
        var logger = NullLogger.Instance;
        var detail = "Sync run completed. Messages: 5, Pages: 2";
        var result = XeniaSensitiveDataGuard.ValidateAndSanitizeAuditDetail(detail, logger);
        Assert.Equal(detail, result);
    }

    [Fact]
    public void ValidateAndSanitizeAuditDetail_RedactsSensitiveDetail()
    {
        var logger = NullLogger.Instance;
        var result = XeniaSensitiveDataGuard.ValidateAndSanitizeAuditDetail(
            "password=hunter2 body_text=very-long-email-body", logger);
        Assert.Equal("[REDACTED-POTENTIAL-SENSITIVE-DATA]", result);
    }

    [Fact]
    public void ValidateAndSanitizeAuditDetail_HandlesNullInput()
    {
        var logger = NullLogger.Instance;
        Assert.Null(XeniaSensitiveDataGuard.ValidateAndSanitizeAuditDetail(null, logger));
    }

    // ── SafeErrorSummary ─────────────────────────────────────────────────────

    [Fact]
    public void SafeErrorSummary_ReturnsEmptyForNullException()
    {
        Assert.Equal(string.Empty, XeniaSensitiveDataGuard.SafeErrorSummary(null));
    }

    [Fact]
    public void SafeErrorSummary_TruncatesLongMessages()
    {
        var ex = new InvalidOperationException(new string('x', 500));
        var result = XeniaSensitiveDataGuard.SafeErrorSummary(ex, maxLength: 50);
        Assert.True(result.Length <= 51); // 50 + "…"
        Assert.EndsWith("…", result);
    }

    [Fact]
    public void SafeErrorSummary_RedactsExceptionWithSensitiveContent()
    {
        var ex = new InvalidOperationException("connection failed: password=secret123");
        var result = XeniaSensitiveDataGuard.SafeErrorSummary(ex);
        Assert.Equal("[exception-message-redacted]", result);
    }

    [Fact]
    public void SafeErrorSummary_PassesSafeExceptionMessage()
    {
        var ex = new InvalidOperationException("Database connection timed out after 30s");
        var result = XeniaSensitiveDataGuard.SafeErrorSummary(ex);
        Assert.Equal("Database connection timed out after 30s", result);
    }
}
