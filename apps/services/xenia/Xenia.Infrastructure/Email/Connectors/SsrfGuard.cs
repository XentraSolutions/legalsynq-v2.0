using System.Net;
using System.Net.Sockets;

namespace Xenia.Infrastructure.Email.Connectors;

/// <summary>
/// SSRF (Server-Side Request Forgery) mitigation for email connector network probes.
///
/// Protection layers:
/// 1. Hostname syntax validation
/// 2. Blocking of known dangerous hostnames (metadata endpoints, localhost)
/// 3. DNS resolution — ALL returned IPs are checked (not just the hostname pattern)
/// 4. IPv4 range blocking: RFC1918, loopback, link-local, reserved, documentation, multicast
/// 5. IPv6 range blocking: ::1, ULA (fc00::/7), link-local, multicast, IPv4-mapped addresses
/// 6. DNS rebinding protection: resolved IP returned, caller MUST use it for connection
/// 7. Allowed-port enforcement
///
/// NEVER disable or bypass this guard. No development-only override exists.
/// Tests that need local addresses use injected test doubles for the resolver.
/// </summary>
public static class SsrfGuard
{
    // Known dangerous hostnames — blocked before DNS resolution
    private static readonly string[] BlockedHostnames =
    [
        "localhost",
        "metadata.google.internal",
        "169.254.169.254",
        "fd00:ec2::254",
        "metadata.azure.internal",
        "metadata.oraclecloud.com",
    ];

    internal static readonly int[] DefaultAllowedPorts = [993, 995, 443, 143, 110];

    /// <summary>
    /// Backward-compatible hostname check (pattern only, no DNS resolution).
    /// For full protection use <see cref="ResolveAndValidateAsync"/>.
    /// </summary>
    public static HostCheckResult CheckHost(string host) => CheckHostname(host);

    /// <summary>
    /// Validates hostname syntax and checks against known blocked names and IP ranges.
    /// Does NOT perform DNS resolution for hostnames — use <see cref="ResolveAndValidateAsync"/> for full validation.
    /// </summary>
    public static HostCheckResult CheckHostname(string host)
    {
        if (string.IsNullOrWhiteSpace(host))
            return HostCheckResult.Blocked("Host is required.");
        if (host.Length > 255)
            return HostCheckResult.Blocked("Host name exceeds maximum length.");

        // Normalize trailing dot (FQDN notation: "localhost." → "localhost")
        var lowered = host.Trim().ToLowerInvariant().TrimEnd('.');

        foreach (var blocked in BlockedHostnames)
            if (lowered == blocked || lowered.EndsWith("." + blocked))
                return HostCheckResult.Blocked("The specified host is not permitted for outbound email connections.");

        if (IPAddress.TryParse(lowered, out var ip))
        {
            var r = CheckIpAddress(ip);
            if (!r.IsAllowed) return r;
        }

        return HostCheckResult.Allowed();
    }

    /// <summary>
    /// Full SSRF validation: DNS resolution + per-IP checks on ALL resolved addresses.
    /// Returns the safe resolved IP for DNS-pinned connection (DNS rebinding protection).
    /// Callers MUST use the returned IP for the actual connection — do not re-resolve.
    /// </summary>
    public static async Task<DnsValidationResult> ResolveAndValidateAsync(
        string host, CancellationToken ct = default)
    {
        var nameCheck = CheckHostname(host);
        if (!nameCheck.IsAllowed)
            return DnsValidationResult.Blocked(nameCheck.SafeReason);

        var lowered = host.Trim().ToLowerInvariant();

        // Already an IP — no DNS needed
        if (IPAddress.TryParse(lowered, out var directIp))
        {
            var r = CheckIpAddress(directIp);
            return r.IsAllowed ? DnsValidationResult.Safe(directIp) : DnsValidationResult.Blocked(r.SafeReason);
        }

        // Resolve and check ALL returned addresses
        IPAddress[] addresses;
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(5));
            addresses = await Dns.GetHostAddressesAsync(lowered, cts.Token);
        }
        catch (OperationCanceledException) { return DnsValidationResult.Blocked("DNS resolution timed out."); }
        catch (SocketException) { return DnsValidationResult.Blocked("DNS resolution failed: host not found."); }
        catch { return DnsValidationResult.Blocked("DNS resolution failed."); }

        if (addresses.Length == 0)
            return DnsValidationResult.Blocked("DNS resolution returned no addresses.");

        foreach (var addr in addresses)
        {
            var check = CheckIpAddress(addr);
            if (!check.IsAllowed)
                return DnsValidationResult.Blocked(check.SafeReason);
        }

        // Prefer IPv4 for IMAP/POP3 compatibility; return for DNS pinning
        var preferred = addresses.OrderBy(a => a.AddressFamily == AddressFamily.InterNetwork ? 0 : 1).First();
        return DnsValidationResult.Safe(preferred);
    }

    /// <summary>Validates port against the allowed list.</summary>
    public static PortCheckResult CheckPort(int port, IReadOnlyList<int>? allowedPorts = null)
    {
        if (port <= 0 || port > 65535)
            return PortCheckResult.Blocked($"Port {port} is out of valid range (1–65535).");
        var allowed = allowedPorts ?? DefaultAllowedPorts;
        if (!allowed.Contains(port))
            return PortCheckResult.Blocked($"Port {port} is not in the tenant's allowed port policy.");
        return PortCheckResult.Allowed();
    }

    /// <summary>
    /// Validates a resolved IP against all blocked ranges.
    /// Handles IPv4, IPv6, and IPv4-mapped IPv6 (::ffff:x.x.x.x).
    /// </summary>
    public static HostCheckResult CheckIpAddress(IPAddress ip)
    {
        if (ip.IsIPv4MappedToIPv6) ip = ip.MapToIPv4();
        if (IPAddress.IsLoopback(ip)) return HostCheckResult.Blocked("Loopback addresses are not permitted.");
        if (ip.AddressFamily == AddressFamily.InterNetwork) return CheckIPv4(ip);
        if (ip.AddressFamily == AddressFamily.InterNetworkV6) return CheckIPv6(ip);
        return HostCheckResult.Blocked("Unsupported address family.");
    }

    private static HostCheckResult CheckIPv4(IPAddress ip)
    {
        var b = ip.GetAddressBytes();
        if (b[0] == 0) return HostCheckResult.Blocked("Reserved address (0.0.0.0/8).");
        if (b[0] == 10) return HostCheckResult.Blocked("Private network (10.0.0.0/8).");
        if (b[0] == 100 && (b[1] & 0xC0) == 64) return HostCheckResult.Blocked("Carrier-grade NAT (100.64.0.0/10).");
        if (b[0] == 127) return HostCheckResult.Blocked("Loopback (127.0.0.0/8).");
        if (b[0] == 169 && b[1] == 254) return HostCheckResult.Blocked("Link-local / cloud metadata (169.254.0.0/16).");
        if (b[0] == 172 && b[1] >= 16 && b[1] <= 31) return HostCheckResult.Blocked("Private network (172.16.0.0/12).");
        if (b[0] == 192 && b[1] == 0 && b[2] == 0) return HostCheckResult.Blocked("IANA special-purpose (192.0.0.0/24).");
        if (b[0] == 192 && b[1] == 0 && b[2] == 2) return HostCheckResult.Blocked("Documentation TEST-NET-1 (192.0.2.0/24).");
        if (b[0] == 192 && b[1] == 168) return HostCheckResult.Blocked("Private network (192.168.0.0/16).");
        if (b[0] == 198 && (b[1] == 18 || b[1] == 19)) return HostCheckResult.Blocked("Benchmarking (198.18.0.0/15).");
        if (b[0] == 198 && b[1] == 51 && b[2] == 100) return HostCheckResult.Blocked("Documentation TEST-NET-2 (198.51.100.0/24).");
        if (b[0] == 203 && b[1] == 0 && b[2] == 113) return HostCheckResult.Blocked("Documentation TEST-NET-3 (203.0.113.0/24).");
        if ((b[0] & 0xF0) == 224) return HostCheckResult.Blocked("Multicast (224.0.0.0/4).");
        if ((b[0] & 0xF0) == 240) return HostCheckResult.Blocked("Reserved/future (240.0.0.0/4).");
        if (b[0] == 255 && b[1] == 255 && b[2] == 255 && b[3] == 255) return HostCheckResult.Blocked("Broadcast (255.255.255.255).");
        return HostCheckResult.Allowed();
    }

    private static HostCheckResult CheckIPv6(IPAddress ip)
    {
        if (ip.IsIPv6LinkLocal) return HostCheckResult.Blocked("IPv6 link-local (fe80::/10).");
        if (ip.IsIPv6SiteLocal) return HostCheckResult.Blocked("IPv6 site-local.");
        if (ip.IsIPv6Multicast) return HostCheckResult.Blocked("IPv6 multicast (ff00::/8).");
        if (ip.Equals(IPAddress.IPv6Loopback)) return HostCheckResult.Blocked("IPv6 loopback (::1).");
        if (ip.Equals(IPAddress.IPv6Any)) return HostCheckResult.Blocked("IPv6 unspecified (::).");
        var b = ip.GetAddressBytes();
        if ((b[0] & 0xFE) == 0xFC) return HostCheckResult.Blocked("IPv6 unique local (fc00::/7).");
        if (b[0] == 0x20 && b[1] == 0x01 && b[2] == 0x0d && b[3] == 0xb8) return HostCheckResult.Blocked("IPv6 documentation (2001:db8::/32).");
        return HostCheckResult.Allowed();
    }
}

public sealed record HostCheckResult
{
    public required bool IsAllowed { get; init; }
    public required string SafeReason { get; init; }
    public static HostCheckResult Allowed() => new() { IsAllowed = true, SafeReason = string.Empty };
    public static HostCheckResult Blocked(string reason) => new() { IsAllowed = false, SafeReason = reason };
}

public sealed record DnsValidationResult
{
    public required bool IsAllowed { get; init; }
    public string? SafeReason { get; init; }
    /// <summary>Validated IP to use for connection (DNS pinning). Use this, never re-resolve.</summary>
    public System.Net.IPAddress? SafeIpAddress { get; init; }
    public static DnsValidationResult Safe(System.Net.IPAddress ip) => new() { IsAllowed = true, SafeIpAddress = ip };
    public static DnsValidationResult Blocked(string reason) => new() { IsAllowed = false, SafeReason = reason };
}

public sealed record PortCheckResult
{
    public required bool IsAllowed { get; init; }
    public required string SafeReason { get; init; }
    public static PortCheckResult Allowed() => new() { IsAllowed = true, SafeReason = string.Empty };
    public static PortCheckResult Blocked(string reason) => new() { IsAllowed = false, SafeReason = reason };
}
