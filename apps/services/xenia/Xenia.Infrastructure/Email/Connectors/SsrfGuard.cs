using System.Net;
using System.Net.Sockets;

namespace Xenia.Infrastructure.Email.Connectors;

/// <summary>
/// SSRF (Server-Side Request Forgery) mitigation for email connector network probes.
///
/// Rejects connection attempts to loopback, link-local, private, and unroutable addresses.
/// All custom IMAP/POP3/ExchangeIMAP host validation must pass through this guard.
///
/// Development exception: explicitly documented in the report. Disabling TLS is blocked
/// at the connector level regardless of this guard.
/// </summary>
internal static class SsrfGuard
{
    private static readonly string[] BlockedHostPatterns = [
        "localhost",
        "metadata.google.internal",
        "169.254.169.254",  // AWS/GCP IMDS
        "fd00:ec2::254",    // AWS IMDS v6
    ];

    /// <summary>
    /// Returns whether the given hostname is allowed for outbound connection probes.
    /// Does NOT perform DNS resolution — hostname pattern check only.
    /// </summary>
    public static HostCheckResult CheckHost(string host)
    {
        if (string.IsNullOrWhiteSpace(host))
            return HostCheckResult.Blocked("Host is required.");

        if (host.Length > 255)
            return HostCheckResult.Blocked("Host name exceeds maximum length.");

        var lowered = host.Trim().ToLowerInvariant();

        foreach (var blocked in BlockedHostPatterns)
        {
            if (lowered == blocked || lowered.EndsWith("." + blocked))
                return HostCheckResult.Blocked(
                    "The specified host is not permitted for outbound email connections.");
        }

        if (IPAddress.TryParse(lowered, out var ip))
        {
            if (IsPrivateOrReserved(ip))
                return HostCheckResult.Blocked(
                    "Private, loopback, and reserved IP addresses are not permitted for email connections.");
        }

        return HostCheckResult.Allowed();
    }

    private static bool IsPrivateOrReserved(IPAddress ip)
    {
        if (IPAddress.IsLoopback(ip)) return true;

        if (ip.AddressFamily == AddressFamily.InterNetwork)
        {
            var bytes = ip.GetAddressBytes();
            return bytes[0] == 10
                || (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31)
                || (bytes[0] == 192 && bytes[1] == 168)
                || (bytes[0] == 169 && bytes[1] == 254)  // link-local
                || bytes[0] == 127;
        }

        if (ip.AddressFamily == AddressFamily.InterNetworkV6)
        {
            if (ip.IsIPv6LinkLocal) return true;
            if (ip.IsIPv6SiteLocal) return true;
            var bytes = ip.GetAddressBytes();
            if (bytes[0] == 0xfc || bytes[0] == 0xfd) return true; // ULA
        }

        return false;
    }
}

internal sealed record HostCheckResult
{
    public required bool IsAllowed { get; init; }
    public required string SafeReason { get; init; }

    public static HostCheckResult Allowed() => new() { IsAllowed = true, SafeReason = string.Empty };
    public static HostCheckResult Blocked(string reason) => new() { IsAllowed = false, SafeReason = reason };
}
