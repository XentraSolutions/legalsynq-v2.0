namespace Commerce.Domain.Billing;

/// <summary>
/// Normalization helper for host-platform keys. Stored and compared in
/// trimmed lowercase form so the unique constraint on
/// (HostPlatformKey, ExternalTenantId) is case-insensitive.
/// </summary>
public static class HostPlatformKey
{
    public static string Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim().ToLowerInvariant();

    public static bool IsValid(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        var k = value.Trim();
        if (k.Length is < 2 or > 64) return false;
        foreach (var c in k)
        {
            if (!(char.IsLetterOrDigit(c) || c is '-' or '_' or '.')) return false;
        }
        return true;
    }
}
