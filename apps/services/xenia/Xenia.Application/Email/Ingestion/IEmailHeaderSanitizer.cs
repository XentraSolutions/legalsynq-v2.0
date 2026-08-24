namespace Xenia.Application.Email.Ingestion;

/// <summary>
/// Sanitizes raw email headers before persistence.
///
/// Rules:
/// - Removes sensitive headers (Authorization, Cookie, X-Api-Key, etc.).
/// - Enforces per-header value length limits.
/// - Enforces total header count limit.
/// - Enforces total serialized size limit.
/// - Normalizes Unicode (NFC).
/// - Omits malformed headers silently.
/// - Case-insensitive header name matching.
///
/// The sanitized result is safe for persistence and API exposure.
/// No credential, token, or internal routing information survives.
/// </summary>
public interface IEmailHeaderSanitizer
{
    /// <summary>
    /// Sanitizes the provided headers dictionary.
    /// Returns a new dictionary containing only safe, length-bounded headers.
    /// </summary>
    IReadOnlyDictionary<string, string> Sanitize(IEnumerable<KeyValuePair<string, string>>? rawHeaders);
}
