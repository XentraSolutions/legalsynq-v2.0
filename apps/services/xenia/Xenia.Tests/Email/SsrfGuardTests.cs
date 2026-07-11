using System.Net;
using Xenia.Infrastructure.Email.Connectors;
using Xunit;

namespace Xenia.Tests.Email;

/// <summary>
/// Tests for SsrfGuard — comprehensive SSRF protection validation.
/// Covers IPv4 ranges, IPv6 ranges, DNS resolution, port policy, and edge cases.
/// </summary>
public sealed class SsrfGuardTests
{
    // ── CheckHostname — blocked hostnames ─────────────────────────────────────

    [Theory]
    [InlineData("localhost")]
    [InlineData("LOCALHOST")]
    [InlineData("localhost.")]
    [InlineData("metadata.google.internal")]
    [InlineData("169.254.169.254")]
    [InlineData("metadata.azure.internal")]
    [InlineData("metadata.oraclecloud.com")]
    public void CheckHostname_KnownBadHostname_IsBlocked(string host)
    {
        var result = SsrfGuard.CheckHostname(host);
        Assert.False(result.IsAllowed, $"{host} should be blocked");
    }

    [Fact]
    public void CheckHostname_Empty_IsBlocked()
    {
        Assert.False(SsrfGuard.CheckHostname("").IsAllowed);
        Assert.False(SsrfGuard.CheckHostname("   ").IsAllowed);
    }

    [Fact]
    public void CheckHostname_TooLong_IsBlocked()
    {
        var long256 = new string('a', 256);
        Assert.False(SsrfGuard.CheckHostname(long256).IsAllowed);
    }

    [Fact]
    public void CheckHostname_ValidPublicHost_IsAllowed()
    {
        Assert.True(SsrfGuard.CheckHostname("mail.example.com").IsAllowed);
        Assert.True(SsrfGuard.CheckHostname("smtp.gmail.com").IsAllowed);
        Assert.True(SsrfGuard.CheckHostname("outlook.office365.com").IsAllowed);
    }

    // ── CheckHostname — IPv4 addresses ────────────────────────────────────────

    [Theory]
    [InlineData("10.0.0.1")]
    [InlineData("10.255.255.255")]
    [InlineData("172.16.0.1")]
    [InlineData("172.31.255.255")]
    [InlineData("192.168.0.1")]
    [InlineData("192.168.255.255")]
    [InlineData("127.0.0.1")]
    [InlineData("127.1.1.1")]
    [InlineData("169.254.0.1")]
    [InlineData("169.254.169.254")]
    [InlineData("0.0.0.1")]
    [InlineData("100.64.0.1")]
    [InlineData("100.127.255.255")]
    [InlineData("192.0.0.1")]
    [InlineData("192.0.2.1")]
    [InlineData("198.18.0.1")]
    [InlineData("198.19.255.255")]
    [InlineData("198.51.100.1")]
    [InlineData("203.0.113.1")]
    [InlineData("224.0.0.1")]
    [InlineData("240.0.0.1")]
    [InlineData("255.255.255.255")]
    public void CheckHostname_PrivateOrReservedIPv4_IsBlocked(string ip)
    {
        var result = SsrfGuard.CheckHostname(ip);
        Assert.False(result.IsAllowed, $"{ip} should be blocked");
    }

    [Theory]
    [InlineData("8.8.8.8")]
    [InlineData("1.1.1.1")]
    [InlineData("208.67.222.222")]
    [InlineData("52.10.20.30")]
    public void CheckHostname_PublicIPv4_IsAllowed(string ip)
    {
        Assert.True(SsrfGuard.CheckHostname(ip).IsAllowed, $"{ip} should be allowed");
    }

    // ── CheckIpAddress — IPv6 ranges ──────────────────────────────────────────

    [Theory]
    [InlineData("::1")]               // loopback
    [InlineData("fe80::1")]           // link-local
    [InlineData("fc00::1")]           // ULA
    [InlineData("fd00::1")]           // ULA
    [InlineData("ff00::1")]           // multicast
    [InlineData("2001:db8::1")]       // documentation
    public void CheckIpAddress_BlockedIPv6_IsBlocked(string ipStr)
    {
        var ip = IPAddress.Parse(ipStr);
        var result = SsrfGuard.CheckIpAddress(ip);
        Assert.False(result.IsAllowed, $"{ipStr} should be blocked");
    }

    [Fact]
    public void CheckIpAddress_IPv4MappedIPv6_PrivateRange_IsBlocked()
    {
        // ::ffff:192.168.1.1 — IPv4-mapped address in private range
        var mapped = IPAddress.Parse("::ffff:192.168.1.1");
        Assert.False(SsrfGuard.CheckIpAddress(mapped).IsAllowed);
    }

    [Fact]
    public void CheckIpAddress_IPv4MappedIPv6_PublicRange_IsAllowed()
    {
        // ::ffff:8.8.8.8 — IPv4-mapped address in public range
        var mapped = IPAddress.Parse("::ffff:8.8.8.8");
        Assert.True(SsrfGuard.CheckIpAddress(mapped).IsAllowed);
    }

    // ── CheckPort ─────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(993)]
    [InlineData(995)]
    [InlineData(443)]
    [InlineData(143)]
    [InlineData(110)]
    public void CheckPort_DefaultAllowedPorts_IsAllowed(int port)
    {
        Assert.True(SsrfGuard.CheckPort(port).IsAllowed);
    }

    [Theory]
    [InlineData(22)]
    [InlineData(80)]
    [InlineData(8080)]
    [InlineData(3306)]
    [InlineData(6379)]
    public void CheckPort_NonEmailPorts_WithDefaults_IsBlocked(int port)
    {
        Assert.False(SsrfGuard.CheckPort(port).IsAllowed);
    }

    [Fact]
    public void CheckPort_OutOfRange_IsBlocked()
    {
        Assert.False(SsrfGuard.CheckPort(0).IsAllowed);
        Assert.False(SsrfGuard.CheckPort(-1).IsAllowed);
        Assert.False(SsrfGuard.CheckPort(65536).IsAllowed);
    }

    [Fact]
    public void CheckPort_CustomAllowedList_RespectsOverride()
    {
        var custom = new[] { 587, 25 };
        Assert.True(SsrfGuard.CheckPort(587, custom).IsAllowed);
        Assert.False(SsrfGuard.CheckPort(993, custom).IsAllowed);
    }

    // ── ResolveAndValidateAsync ───────────────────────────────────────────────

    [Theory]
    [InlineData("localhost")]
    [InlineData("metadata.google.internal")]
    public async Task ResolveAndValidateAsync_BlockedHostname_IsBlocked(string host)
    {
        var result = await SsrfGuard.ResolveAndValidateAsync(host);
        Assert.False(result.IsAllowed);
        Assert.NotEmpty(result.SafeReason ?? "");
    }

    [Theory]
    [InlineData("10.0.0.1")]
    [InlineData("192.168.1.1")]
    [InlineData("172.20.0.1")]
    public async Task ResolveAndValidateAsync_PrivateIPv4_IsBlocked(string ip)
    {
        var result = await SsrfGuard.ResolveAndValidateAsync(ip);
        Assert.False(result.IsAllowed);
    }

    [Theory]
    [InlineData("::1")]
    [InlineData("fe80::1")]
    [InlineData("fc00::dead:beef")]
    public async Task ResolveAndValidateAsync_PrivateIPv6_IsBlocked(string ip)
    {
        var result = await SsrfGuard.ResolveAndValidateAsync(ip);
        Assert.False(result.IsAllowed);
    }

    [Fact]
    public async Task ResolveAndValidateAsync_PublicIP_ReturnsSafeIp()
    {
        var result = await SsrfGuard.ResolveAndValidateAsync("8.8.8.8");
        Assert.True(result.IsAllowed);
        Assert.NotNull(result.SafeIpAddress);
        Assert.Equal(IPAddress.Parse("8.8.8.8"), result.SafeIpAddress);
    }

    [Fact]
    public async Task ResolveAndValidateAsync_NonexistentHost_IsBlocked()
    {
        var result = await SsrfGuard.ResolveAndValidateAsync("this-host-does-not-exist.invalid");
        Assert.False(result.IsAllowed);
        Assert.NotEmpty(result.SafeReason ?? "");
    }

    // ── CheckHost backward-compat ─────────────────────────────────────────────

    [Fact]
    public void CheckHost_BackwardCompatAlias_WorksCorrectly()
    {
        // CheckHost is the old API — must still function identically to CheckHostname
        Assert.False(SsrfGuard.CheckHost("localhost").IsAllowed);
        Assert.True(SsrfGuard.CheckHost("mail.example.com").IsAllowed);
    }
}
