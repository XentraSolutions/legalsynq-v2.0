using System.Security.Cryptography;
using System.Text;
using Commerce.Application.Common.Exceptions;

namespace Commerce.Infrastructure.Payments.Stripe;

/// <summary>
/// Implements the Stripe-compatible webhook signature scheme:
/// header is <c>t=&lt;unix_seconds&gt;,v1=&lt;hex_hmac_sha256&gt;</c>; the
/// signed payload is <c>"{t}.{rawBody}"</c>; signatures are computed
/// with HMAC-SHA256 keyed by the webhook secret and compared in
/// constant time. A configurable timestamp tolerance window (default
/// 5 minutes) protects against replay.
/// </summary>
public static class StripeSignatureVerifier
{
    public const string HeaderName = "Stripe-Signature";
    private const string SchemeKey = "v1";
    private const string TimestampKey = "t";

    public static void Verify(string rawBody, string? signatureHeader, string? webhookSecret, int toleranceSeconds, DateTime nowUtc)
    {
        if (string.IsNullOrWhiteSpace(webhookSecret))
            throw new PaymentProviderConfigurationException("Stripe", "WebhookSecret");
        if (string.IsNullOrWhiteSpace(signatureHeader))
            throw new InvalidWebhookSignatureException("Stripe");

        long? t = null;
        var v1s = new List<string>();
        foreach (var part in signatureHeader.Split(','))
        {
            var idx = part.IndexOf('=');
            if (idx <= 0) continue;
            var key = part[..idx].Trim();
            var value = part[(idx + 1)..].Trim();
            if (key == TimestampKey && long.TryParse(value, out var parsed)) t = parsed;
            else if (key == SchemeKey) v1s.Add(value);
        }
        if (t is null || v1s.Count == 0)
            throw new InvalidWebhookSignatureException("Stripe");

        var nowEpoch = new DateTimeOffset(nowUtc, TimeSpan.Zero).ToUnixTimeSeconds();
        if (Math.Abs(nowEpoch - t.Value) > toleranceSeconds)
            throw new InvalidWebhookSignatureException("Stripe");

        var signedPayload = $"{t.Value}.{rawBody}";
        var keyBytes = Encoding.UTF8.GetBytes(webhookSecret);
        using var hmac = new HMACSHA256(keyBytes);
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(signedPayload));
        var expectedHex = Convert.ToHexString(hash).ToLowerInvariant();

        foreach (var candidate in v1s)
        {
            if (FixedTimeEquals(expectedHex, candidate.ToLowerInvariant())) return;
        }
        throw new InvalidWebhookSignatureException("Stripe");
    }

    /// <summary>
    /// Builds a valid <c>Stripe-Signature</c> header for tests. Not used
    /// by production code, but lives here to keep the algorithm in one
    /// place.
    /// </summary>
    public static string SignForTesting(string rawBody, string secret, DateTime atUtc)
    {
        var t = new DateTimeOffset(atUtc, TimeSpan.Zero).ToUnixTimeSeconds();
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes($"{t}.{rawBody}"));
        var hex = Convert.ToHexString(hash).ToLowerInvariant();
        return $"t={t},v1={hex}";
    }

    private static bool FixedTimeEquals(string a, string b)
    {
        if (a.Length != b.Length) return false;
        var diff = 0;
        for (var i = 0; i < a.Length; i++) diff |= a[i] ^ b[i];
        return diff == 0;
    }
}
