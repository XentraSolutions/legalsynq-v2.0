using System.Text;
using Xenia.Application.Email.Ingestion;

namespace Xenia.Infrastructure.Email;

/// <summary>
/// Sanitizes raw email headers before persistence.
///
/// Policy:
/// - DENIED headers (always removed): Authorization, Proxy-Authorization, Cookie, Set-Cookie,
///   X-Api-Key, X-Auth-Token, X-Access-Token, X-Session-Token, Bearer, X-Forwarded-Authorization,
///   X-Internal-Secret, X-Service-Token, any header with "secret" or "credential" in name.
/// - ALLOWED infrastructure headers retained as informational (no interpretation):
///   Received, Authentication-Results, DKIM-Signature, ARC-*, X-Originating-IP.
///   Internal routing headers (X-Forwarded-*, X-Real-IP) are REMOVED.
/// - Max headers count: 50
/// - Max individual value length: 1024 characters (values truncated, not removed)
/// - Max total serialized size: 65536 bytes
/// - Unicode normalization: NFC
/// - Case-insensitive header name matching
/// - Malformed headers (null/empty name or value) silently omitted
/// </summary>
internal sealed class EmailHeaderSanitizer : IEmailHeaderSanitizer
{
    private const int MaxHeaderCount   = 50;
    private const int MaxValueLength   = 1024;
    private const int MaxTotalBytes    = 65536;

    // Headers that are always denied — case-insensitive
    private static readonly HashSet<string> DeniedExact = new(StringComparer.OrdinalIgnoreCase)
    {
        "Authorization",
        "Proxy-Authorization",
        "Cookie",
        "Set-Cookie",
        "X-Api-Key",
        "X-Auth-Token",
        "X-Access-Token",
        "X-Session-Token",
        "X-Internal-Secret",
        "X-Service-Token",
        "X-Forwarded-Authorization",
        "X-Forwarded-For",
        "X-Forwarded-Host",
        "X-Forwarded-Proto",
        "X-Real-IP",
        "WWW-Authenticate",
        "Proxy-Authenticate",
    };

    // Substrings in header names that trigger denial — case-insensitive
    private static readonly string[] DeniedSubstrings =
    [
        "secret",
        "credential",
        "password",
        "token",
        "auth",
    ];

    // Headers in the denied-substring list but that are explicitly allowed
    private static readonly HashSet<string> AllowedDespiteSubstring = new(StringComparer.OrdinalIgnoreCase)
    {
        "Authentication-Results",
        "DKIM-Signature",
        "ARC-Authentication-Results",
        "ARC-Message-Signature",
        "ARC-Seal",
    };

    public IReadOnlyDictionary<string, string> Sanitize(IEnumerable<KeyValuePair<string, string>>? rawHeaders)
    {
        if (rawHeaders is null)
            return new Dictionary<string, string>(0);

        var result    = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        int totalBytes= 0;
        int count     = 0;

        foreach (var (name, value) in rawHeaders)
        {
            if (count >= MaxHeaderCount) break;
            if (totalBytes >= MaxTotalBytes) break;

            // Reject malformed
            if (string.IsNullOrWhiteSpace(name) || value is null) continue;

            // Normalize name
            var normalizedName = NormalizeString(name.Trim());

            // Apply denylist
            if (IsDenied(normalizedName)) continue;

            // Normalize and bound value
            var normalizedValue = NormalizeString(value);
            if (normalizedValue.Length > MaxValueLength)
                normalizedValue = normalizedValue[..MaxValueLength];

            var entryBytes = Encoding.UTF8.GetByteCount(normalizedName) +
                             Encoding.UTF8.GetByteCount(normalizedValue) + 2;

            if (totalBytes + entryBytes > MaxTotalBytes) break;

            result[normalizedName]  = normalizedValue;
            totalBytes             += entryBytes;
            count++;
        }

        return result;
    }

    private static bool IsDenied(string name)
    {
        if (DeniedExact.Contains(name)) return true;

        // Allow explicitly permitted headers even if they contain a denied substring
        if (AllowedDespiteSubstring.Contains(name)) return false;

        // Deny if name contains sensitive substrings
        foreach (var sub in DeniedSubstrings)
        {
            if (name.Contains(sub, StringComparison.OrdinalIgnoreCase)) return true;
        }

        return false;
    }

    private static string NormalizeString(string s)
        => string.IsNullOrEmpty(s) ? s : s.Normalize(NormalizationForm.FormC);
}
