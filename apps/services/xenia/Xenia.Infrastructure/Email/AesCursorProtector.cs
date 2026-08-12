using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using Xenia.Application.Email.Ingestion;

namespace Xenia.Infrastructure.Email;

/// <summary>
/// AES-256-GCM cursor protector.
///
/// Protects opaque provider cursor values (delta tokens, history IDs, UID strings)
/// before persisting them in the database. The protection binds each value to a
/// (TenantId, SourceId) pair — decryption with the wrong binding fails.
///
/// Format: "v1.{base64-nonce}.{base64-ciphertext}.{base64-tag}"
///
/// Development: falls back to a zero-key when XeniaCursorProtection:Key is absent.
///              Zero-key is NOT safe for production — it is documented and logged at startup.
/// Production: set XeniaCursorProtection:Key to a 64-character hex string (32 bytes).
/// </summary>
internal sealed class AesCursorProtector : IProviderCursorProtector
{
    private const string CurrentVersion = "v1";
    private const int NonceSize = 12;
    private const int TagSize   = 16;
    private const int KeySize   = 32;

    private readonly byte[] _key;
    private readonly bool _usingDevFallback;

    public AesCursorProtector(IConfiguration configuration)
    {
        var keyHex = configuration["XeniaCursorProtection:Key"];
        if (string.IsNullOrWhiteSpace(keyHex))
        {
            _key             = new byte[KeySize];
            _usingDevFallback= true;
        }
        else
        {
            _key = Convert.FromHexString(keyHex);
            if (_key.Length != KeySize)
                throw new InvalidOperationException(
                    "XeniaCursorProtection:Key must be exactly 64 hex characters (32 bytes).");
            _usingDevFallback = false;
        }
    }

    public string GetVersion() => CurrentVersion;

    public bool IsUsingDevFallbackKey => _usingDevFallback;

    public Task<string> ProtectAsync(
        string rawCursor,
        Guid tenantId,
        Guid emailSourceId,
        CancellationToken ct = default)
    {
        var nonce      = new byte[NonceSize];
        RandomNumberGenerator.Fill(nonce);

        var plaintext      = Encoding.UTF8.GetBytes(rawCursor);
        var ciphertext     = new byte[plaintext.Length];
        var tag            = new byte[TagSize];
        var additionalData = BuildAdditionalData(tenantId, emailSourceId);

        using var aes = new AesGcm(_key, TagSize);
        aes.Encrypt(nonce, plaintext, ciphertext, tag, additionalData);

        var result =
            $"{CurrentVersion}.{Convert.ToBase64String(nonce)}.{Convert.ToBase64String(ciphertext)}.{Convert.ToBase64String(tag)}";

        return Task.FromResult(result);
    }

    public Task<string?> UnprotectAsync(
        string protectedCursor,
        Guid tenantId,
        Guid emailSourceId,
        CancellationToken ct = default)
    {
        try
        {
            var parts = protectedCursor.Split('.');
            if (parts.Length != 4 || parts[0] != CurrentVersion)
                return Task.FromResult<string?>(null);

            var nonce      = Convert.FromBase64String(parts[1]);
            var ciphertext = Convert.FromBase64String(parts[2]);
            var tag        = Convert.FromBase64String(parts[3]);
            var plaintext  = new byte[ciphertext.Length];
            var additionalData = BuildAdditionalData(tenantId, emailSourceId);

            using var aes = new AesGcm(_key, TagSize);
            aes.Decrypt(nonce, ciphertext, tag, plaintext, additionalData);

            return Task.FromResult<string?>(Encoding.UTF8.GetString(plaintext));
        }
        catch (CryptographicException)
        {
            return Task.FromResult<string?>(null);
        }
        catch
        {
            return Task.FromResult<string?>(null);
        }
    }

    private static byte[] BuildAdditionalData(Guid tenantId, Guid emailSourceId) =>
        Encoding.UTF8.GetBytes($"xenia:cursor:{tenantId:N}:{emailSourceId:N}");
}
