namespace Xenia.Application.Email.Ingestion;

/// <summary>
/// Sanitizes email HTML bodies before storage and display.
///
/// All email HTML must be sanitized before:
/// 1. Persistence in the database
/// 2. Rendering in the Control Center UI
///
/// The sanitizer removes or neutralizes:
/// - script elements and inline event handlers
/// - iframe, frame, form, object, embed, base
/// - javascript: and vbscript: URL schemes
/// - active SVG content
/// - unsafe data: URLs
/// - remote image sources (optional — controlled by BlockRemoteImages setting)
/// - tracking pixels and external resource loads
/// - meta-refresh directives
///
/// Raw HTML must not be rendered directly.
/// A plain-text fallback must always be available.
/// </summary>
public interface IEmailHtmlSanitizer
{
    /// <summary>
    /// Sanitizes an HTML string.
    /// Returns an empty string if the input is empty or null.
    /// Never throws — returns empty string on unexpected failures.
    /// </summary>
    string Sanitize(string? html);

    /// <summary>
    /// Returns whether the sanitizer is configured to block remote images.
    /// </summary>
    bool BlocksRemoteImages { get; }
}
