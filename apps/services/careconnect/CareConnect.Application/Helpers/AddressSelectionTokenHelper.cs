using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace CareConnect.Application.Helpers;

public static class AddressSelectionTokenHelper
{
    public static AddressSelectionClaims? Decode(string? rawToken, string? secret, DateTimeOffset? now = null)
    {
        if (string.IsNullOrWhiteSpace(rawToken) || string.IsNullOrWhiteSpace(secret))
            return null;

        try
        {
            var dot = rawToken.LastIndexOf('.');
            if (dot <= 0 || dot == rawToken.Length - 1)
                return null;

            var body = rawToken[..dot];
            var sig  = rawToken[(dot + 1)..];

            var expected = ComputeSignature(body, secret);
            var sigBytes = Convert.FromBase64String(NormalizeBase64Url(sig));
            var expBytes = Convert.FromBase64String(NormalizeBase64Url(expected));
            if (sigBytes.Length != expBytes.Length || !CryptographicOperations.FixedTimeEquals(sigBytes, expBytes))
                return null;

            var claims = JsonSerializer.Deserialize<AddressSelectionClaims>(
                Encoding.UTF8.GetString(Convert.FromBase64String(NormalizeBase64Url(body))));
            if (claims is null)
                return null;

            var nowUnix = (now ?? DateTimeOffset.UtcNow).ToUnixTimeSeconds();
            return claims.Exp < nowUnix ? null : claims;
        }
        catch
        {
            return null;
        }
    }

    public static bool MatchesAddress(
        AddressSelectionClaims claims,
        string? addressLine1,
        string? city,
        string? state,
        string? postalCode)
    {
        return string.Equals(claims.AddressLine1, addressLine1?.Trim() ?? string.Empty, StringComparison.Ordinal)
            && string.Equals(claims.City,         city?.Trim()         ?? string.Empty, StringComparison.Ordinal)
            && string.Equals(claims.State,        state?.Trim()        ?? string.Empty, StringComparison.Ordinal)
            && string.Equals(claims.PostalCode,   postalCode?.Trim()   ?? string.Empty, StringComparison.Ordinal);
    }

    private static string ComputeSignature(string body, string secret)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        return ToBase64Url(hmac.ComputeHash(Encoding.UTF8.GetBytes(body)));
    }

    private static string NormalizeBase64Url(string value)
    {
        var normalized = value.Replace('-', '+').Replace('_', '/');
        return normalized.PadRight(normalized.Length + (4 - normalized.Length % 4) % 4, '=');
    }

    private static string ToBase64Url(byte[] bytes)
        => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}

public sealed record AddressSelectionClaims(
    string AddressLine1,
    string City,
    string State,
    string PostalCode,
    long Exp);
