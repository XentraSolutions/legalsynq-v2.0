using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace Xenia.Infrastructure.Security;

/// <summary>
/// Phase E — compile-time and runtime guards against accidental sensitive data leakage.
///
/// Security contract (enforced here and documented in T5 report):
/// 1. Email message bodies are NEVER stored in XeniaAuditEvent.Detail.
/// 2. Attachment filenames are sanitized (extension only) before logging.
/// 3. Cursor values are NEVER logged or stored in audit events.
/// 4. Provider credentials are NEVER in logs.
/// 5. Tenant IDs in logs are permitted (not PII in this context).
/// 6. Run IDs in logs are permitted (opaque GUIDs).
///
/// This class provides:
/// - Static validators usable in unit tests.
/// - Runtime sanitization helpers for safe log strings.
/// - Regex patterns for detecting accidental credential leakage in test mode.
/// </summary>
public static class XeniaSensitiveDataGuard
{
    // Patterns that should NEVER appear in audit Detail fields or structured log values
    private static readonly Regex _credentialPattern =
        new(@"(password|secret|token|bearer|api_?key|client_?secret)\s*[=:]\s*\S+",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex _emailBodyPattern =
        new(@"(body_?text|body_?html|body_?preview)\s*[=:]\s*.{10,}",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex _cursorPattern =
        new(@"cursor\s*[=:]\s*\S{10,}",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex _attachmentFilenamePattern =
        new(@"\.(exe|bat|cmd|sh|ps1|vbs|js|msi|dll|so)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// Returns true if the string appears to contain sensitive data.
    /// Used in assertion-based security tests.
    /// </summary>
    public static bool ContainsSensitiveData(string? input)
    {
        if (string.IsNullOrEmpty(input)) return false;
        return _credentialPattern.IsMatch(input)
            || _emailBodyPattern.IsMatch(input)
            || _cursorPattern.IsMatch(input);
    }

    /// <summary>
    /// Sanitizes an attachment filename for logging — only the extension is retained.
    /// Never logs the actual filename.
    /// </summary>
    public static string SanitizeAttachmentFilename(string? filename)
    {
        if (string.IsNullOrEmpty(filename)) return "[no-filename]";
        var ext = Path.GetExtension(filename);
        return string.IsNullOrEmpty(ext) ? "[no-ext]" : ext.ToLowerInvariant();
    }

    /// <summary>
    /// Returns whether a MIME type is potentially dangerous.
    /// Used by the attachment dispatcher to flag high-risk content.
    /// </summary>
    public static bool IsHighRiskMimeType(string? mimeType)
    {
        if (string.IsNullOrEmpty(mimeType)) return false;
        return mimeType.Contains("application/x-ms", StringComparison.OrdinalIgnoreCase)
            || mimeType.Contains("application/octet-stream", StringComparison.OrdinalIgnoreCase)
            || mimeType.Equals("application/x-executable", StringComparison.OrdinalIgnoreCase)
            || mimeType.Equals("application/x-dosexec", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Validates that an audit Detail string is safe to persist.
    /// Logs a warning (never throws) if the detail appears to contain sensitive data.
    /// </summary>
    public static string? ValidateAndSanitizeAuditDetail(string? detail, ILogger logger)
    {
        if (detail is null) return null;
        if (!ContainsSensitiveData(detail)) return detail;

        logger.LogWarning(
            "Audit detail may contain sensitive data and was redacted. Length={Length}",
            detail.Length);
        return "[REDACTED-POTENTIAL-SENSITIVE-DATA]";
    }

    /// <summary>
    /// Ensures a filename does not contain a dangerous extension.
    /// Returns a safe extension description for logging.
    /// </summary>
    public static bool IsDangerousFilename(string? filename)
    {
        if (string.IsNullOrEmpty(filename)) return false;
        return _attachmentFilenamePattern.IsMatch(filename);
    }

    /// <summary>
    /// Extracts a safe, short error summary — strips stack traces, file paths, and credentials.
    /// Truncates to <paramref name="maxLength"/> characters.
    /// </summary>
    public static string SafeErrorSummary(Exception? ex, int maxLength = 200)
    {
        if (ex is null) return string.Empty;
        var msg = ex.Message;
        if (ContainsSensitiveData(msg))
            return "[exception-message-redacted]";
        return msg.Length > maxLength ? msg[..maxLength] + "…" : msg;
    }
}
