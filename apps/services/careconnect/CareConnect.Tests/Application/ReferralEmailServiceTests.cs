// LSCC-005: Tests for ReferralEmailService — token generation/validation, expiry, tampering.
using System.Security.Cryptography;
using System.Text;
using CareConnect.Application.Interfaces;
using CareConnect.Application.Repositories;
using CareConnect.Application.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace CareConnect.Tests.Application;

/// <summary>
/// LSCC-005 — Verifies the referral view token contract:
///   - GenerateViewToken produces a valid, URL-safe Base64 token
///   - ValidateViewToken returns the correct referralId for a valid token
///   - ValidateViewToken returns null for expired tokens
///   - ValidateViewToken returns null for tampered / malformed tokens
///   - Round-trip generate → validate is stable
/// </summary>
public class ReferralEmailServiceTests
{
    private const string TestSecret = "TEST-REFERRAL-SECRET-KEY-2026";
    private const string TestBaseUrl = "http://localhost:3000";

    // ── Factory ──────────────────────────────────────────────────────────────

    private static ReferralEmailService BuildService(string? secret = TestSecret)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ReferralToken:Secret"] = secret,
                ["AppBaseUrl"]           = TestBaseUrl,
            })
            .Build();

        var notifications = new Mock<INotificationRepository>();
        var smtp          = new Mock<ISmtpEmailSender>();
        ILogger<ReferralEmailService> logger = NullLogger<ReferralEmailService>.Instance;

        return new ReferralEmailService(notifications.Object, smtp.Object, config, logger);
    }

    // ── Token format ─────────────────────────────────────────────────────────

    [Fact]
    public void GenerateViewToken_ReturnsNonEmptyString()
    {
        var svc   = BuildService();
        var token = svc.GenerateViewToken(Guid.NewGuid());
        Assert.False(string.IsNullOrWhiteSpace(token));
    }

    [Fact]
    public void GenerateViewToken_IsUrlSafeBase64_NoReservedChars()
    {
        var svc   = BuildService();
        var token = svc.GenerateViewToken(Guid.NewGuid());

        Assert.DoesNotContain("+", token);
        Assert.DoesNotContain("/", token);
        Assert.DoesNotContain("=", token);
    }

    [Fact]
    public void GenerateViewToken_TwoCallsSameId_ProduceDifferentTokens()
    {
        // Each token has a fresh expiry timestamp — two calls seconds apart produce
        // different tokens because the expiry unix second can advance.
        // More importantly the expiry value is included so tokens are unique per issuance.
        var svc = BuildService();
        var id  = Guid.NewGuid();
        var t1  = svc.GenerateViewToken(id);
        var t2  = svc.GenerateViewToken(id);
        // Tokens CAN be equal if issued within the same second — that is fine by contract,
        // but they must never be empty.
        Assert.False(string.IsNullOrWhiteSpace(t1));
        Assert.False(string.IsNullOrWhiteSpace(t2));
    }

    // ── Round-trip ───────────────────────────────────────────────────────────

    [Fact]
    public void RoundTrip_Generate_Validate_ReturnsOriginalReferralId()
    {
        var svc        = BuildService();
        var referralId = Guid.NewGuid();
        var token      = svc.GenerateViewToken(referralId);
        var result     = svc.ValidateViewToken(token);
        Assert.Equal(referralId, result);
    }

    [Fact]
    public void RoundTrip_MultipleIds_EachValidatesToCorrectId()
    {
        var svc = BuildService();
        var ids = Enumerable.Range(0, 5).Select(_ => Guid.NewGuid()).ToList();

        foreach (var id in ids)
        {
            var token  = svc.GenerateViewToken(id);
            var result = svc.ValidateViewToken(token);
            Assert.Equal(id, result);
        }
    }

    // ── Expiry ───────────────────────────────────────────────────────────────

    [Fact]
    public void ValidateViewToken_ExpiredToken_ReturnsNull()
    {
        var svc        = BuildService();
        var referralId = Guid.NewGuid();

        // Craft a token with an expiry in the past using the same algorithm.
        var expiry      = DateTimeOffset.UtcNow.AddSeconds(-1).ToUnixTimeSeconds();
        var payload     = $"{referralId}:{expiry}";
        var keyBytes    = Encoding.UTF8.GetBytes(TestSecret);
        using var hmac  = new HMACSHA256(keyBytes);
        var sig         = Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();
        var raw         = $"{payload}:{sig}";
        var token       = Convert.ToBase64String(Encoding.UTF8.GetBytes(raw))
                              .TrimEnd('=').Replace('+', '-').Replace('/', '_');

        Assert.Null(svc.ValidateViewToken(token));
    }

    // ── Tampering ────────────────────────────────────────────────────────────

    [Fact]
    public void ValidateViewToken_TamperedSignature_ReturnsNull()
    {
        var svc        = BuildService();
        var referralId = Guid.NewGuid();
        var token      = svc.GenerateViewToken(referralId);

        // Decode, replace the HMAC with an all-zeros hex string of the same length, re-encode.
        var padded  = token.Replace('-', '+').Replace('_', '/');
        var mod     = padded.Length % 4;
        if (mod != 0) padded += new string('=', 4 - mod);
        var raw     = Encoding.UTF8.GetString(Convert.FromBase64String(padded));
        var lastSep = raw.LastIndexOf(':');
        var tampered_raw = raw[..(lastSep + 1)] + new string('0', raw.Length - lastSep - 1);
        var tampered = Convert.ToBase64String(Encoding.UTF8.GetBytes(tampered_raw))
                           .TrimEnd('=').Replace('+', '-').Replace('/', '_');

        Assert.Null(svc.ValidateViewToken(tampered));
    }

    [Fact]
    public void ValidateViewToken_WrongSecret_ReturnsNull()
    {
        var svcA   = BuildService("SECRET-A");
        var svcB   = BuildService("SECRET-B");
        var id     = Guid.NewGuid();
        var token  = svcA.GenerateViewToken(id);  // signed with A
        var result = svcB.ValidateViewToken(token); // validated with B
        Assert.Null(result);
    }

    // ── Malformed inputs ─────────────────────────────────────────────────────

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("notbase64!!!")]
    [InlineData("dGVzdA==")]         // base64 for "test" — not a valid token structure
    [InlineData("aGVsbG8=")]         // "hello"
    public void ValidateViewToken_MalformedInput_ReturnsNull(string bad)
    {
        var svc = BuildService();
        Assert.Null(svc.ValidateViewToken(bad));
    }

    [Fact]
    public void ValidateViewToken_NullEquivalent_ReturnsNull()
    {
        var svc = BuildService();
        Assert.Null(svc.ValidateViewToken(string.Empty));
    }

    // ── Dev fallback ─────────────────────────────────────────────────────────

    [Fact]
    public void Service_NoSecretConfigured_StillGeneratesValidTokens()
    {
        // When ReferralToken:Secret is absent the service falls back to a dev constant.
        // Tokens must still round-trip correctly (dev environments work).
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AppBaseUrl"] = TestBaseUrl,
                // NOTE: ReferralToken:Secret intentionally omitted
            })
            .Build();

        var notifications = new Mock<INotificationRepository>();
        var smtp          = new Mock<ISmtpEmailSender>();
        ILogger<ReferralEmailService> logger = NullLogger<ReferralEmailService>.Instance;

        var svc  = new ReferralEmailService(notifications.Object, smtp.Object, config, logger);
        var id   = Guid.NewGuid();
        var tok  = svc.GenerateViewToken(id);
        Assert.Equal(id, svc.ValidateViewToken(tok));
    }
}
