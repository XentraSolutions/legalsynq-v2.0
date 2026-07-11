using Microsoft.Extensions.Configuration;
using Xenia.Infrastructure.Email;

namespace Xenia.Tests.Email.Ingestion;

/// <summary>
/// Unit tests for AesCursorProtector.
///
/// Criteria verified:
/// - Protection round-trips cleanly (protect → unprotect → original value)
/// - Binding is enforced: wrong tenantId or sourceId → null on unprotect
/// - Tampered ciphertext → null on unprotect
/// - Unprotect on unknown version prefix → null
/// - Empty/null cursors handled correctly
/// - Dev fallback key used when config absent
/// - Production key (32-byte hex) used when configured
/// </summary>
public sealed class CursorProtectorTests
{
    private static AesCursorProtector CreateProtector(string? keyHex = null)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(keyHex is not null
                ? new Dictionary<string, string?> { ["XeniaCursorProtection:Key"] = keyHex }
                : new Dictionary<string, string?>())
            .Build();
        return new AesCursorProtector(config);
    }

    [Fact]
    public async Task RoundTrip_DevFallback_ReturnsOriginalValue()
    {
        var protector  = CreateProtector();
        var tenantId   = Guid.NewGuid();
        var sourceId   = Guid.NewGuid();
        const string raw = "delta_token_abc123";

        var protected_  = await protector.ProtectAsync(raw, tenantId, sourceId);
        var unprotected = await protector.UnprotectAsync(protected_, tenantId, sourceId);

        Assert.Equal(raw, unprotected);
    }

    [Fact]
    public async Task RoundTrip_ConfiguredKey_ReturnsOriginalValue()
    {
        var keyHex    = new string('a', 64); // 32 bytes
        var protector = CreateProtector(keyHex);
        var tenantId  = Guid.NewGuid();
        var sourceId  = Guid.NewGuid();
        const string raw = "1234567890:99";

        var protected_  = await protector.ProtectAsync(raw, tenantId, sourceId);
        var unprotected = await protector.UnprotectAsync(protected_, tenantId, sourceId);

        Assert.Equal(raw, unprotected);
    }

    [Fact]
    public async Task WrongTenantId_ReturnsNull()
    {
        var protector = CreateProtector();
        var tenantId  = Guid.NewGuid();
        var sourceId  = Guid.NewGuid();

        var protected_ = await protector.ProtectAsync("some_cursor", tenantId, sourceId);
        var result     = await protector.UnprotectAsync(protected_, Guid.NewGuid(), sourceId);

        Assert.Null(result);
    }

    [Fact]
    public async Task WrongSourceId_ReturnsNull()
    {
        var protector = CreateProtector();
        var tenantId  = Guid.NewGuid();
        var sourceId  = Guid.NewGuid();

        var protected_ = await protector.ProtectAsync("some_cursor", tenantId, sourceId);
        var result     = await protector.UnprotectAsync(protected_, tenantId, Guid.NewGuid());

        Assert.Null(result);
    }

    [Fact]
    public async Task TamperedCiphertext_ReturnsNull()
    {
        var protector = CreateProtector();
        var tenantId  = Guid.NewGuid();
        var sourceId  = Guid.NewGuid();

        var protected_ = await protector.ProtectAsync("original_cursor", tenantId, sourceId);

        // Tamper: flip a character in the ciphertext segment
        var parts = protected_.Split('.');
        if (parts.Length >= 3)
        {
            var ciphertextBytes = Convert.FromBase64String(parts[2]);
            ciphertextBytes[0] ^= 0xFF;
            parts[2] = Convert.ToBase64String(ciphertextBytes);
        }
        var tampered = string.Join('.', parts);

        var result = await protector.UnprotectAsync(tampered, tenantId, sourceId);
        Assert.Null(result);
    }

    [Fact]
    public async Task UnknownVersion_ReturnsNull()
    {
        var protector = CreateProtector();
        var result = await protector.UnprotectAsync(
            "v99.AAAA.AAAA.AAAA", Guid.NewGuid(), Guid.NewGuid());
        Assert.Null(result);
    }

    [Fact]
    public async Task MalformedString_ReturnsNull()
    {
        var protector = CreateProtector();
        var result = await protector.UnprotectAsync("notavalidprotectedstring", Guid.NewGuid(), Guid.NewGuid());
        Assert.Null(result);
    }

    [Fact]
    public async Task EmptyString_ProtectedAndRoundTrips()
    {
        var protector = CreateProtector();
        var tenantId  = Guid.NewGuid();
        var sourceId  = Guid.NewGuid();

        var protected_ = await protector.ProtectAsync(string.Empty, tenantId, sourceId);
        var result     = await protector.UnprotectAsync(protected_, tenantId, sourceId);

        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public async Task ProtectTwice_ProducesDistinctCiphertexts()
    {
        var protector = CreateProtector();
        var tenantId  = Guid.NewGuid();
        var sourceId  = Guid.NewGuid();

        var p1 = await protector.ProtectAsync("cursor_value", tenantId, sourceId);
        var p2 = await protector.ProtectAsync("cursor_value", tenantId, sourceId);

        // Different nonces → different ciphertexts
        Assert.NotEqual(p1, p2);

        // But both decrypt to the same value
        var u1 = await protector.UnprotectAsync(p1, tenantId, sourceId);
        var u2 = await protector.UnprotectAsync(p2, tenantId, sourceId);
        Assert.Equal("cursor_value", u1);
        Assert.Equal("cursor_value", u2);
    }

    [Fact]
    public void GetVersion_ReturnsV1()
    {
        var protector = CreateProtector();
        Assert.Equal("v1", protector.GetVersion());
    }

    [Fact]
    public void InvalidKeyLength_Throws()
    {
        // 30 bytes = 60 hex chars — invalid
        var shortKey = new string('b', 60);
        Assert.Throws<InvalidOperationException>(() => CreateProtector(shortKey));
    }

    [Fact]
    public async Task LargeCursor_RoundTrips()
    {
        var protector = CreateProtector();
        var tenantId  = Guid.NewGuid();
        var sourceId  = Guid.NewGuid();
        var largeCursor = new string('x', 4000);

        var protected_ = await protector.ProtectAsync(largeCursor, tenantId, sourceId);
        var result     = await protector.UnprotectAsync(protected_, tenantId, sourceId);

        Assert.Equal(largeCursor, result);
    }
}
