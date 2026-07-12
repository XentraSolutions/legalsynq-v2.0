using Xenia.Application;
using Xenia.Domain;

namespace Xenia.Infrastructure.Security;

internal sealed class InMemoryAiCredentialStore : IAiCredentialStore
{
    private readonly Lock _syncRoot = new();
    private readonly Dictionary<Guid, CredentialRecord> _records = [];

    public XeniaProviderCredentialRecord Save(
        Guid providerConfigurationId,
        XeniaCredentialStorageMode storageMode,
        string? apiKey,
        string? externalSecretReference)
    {
        var now = DateTime.UtcNow;
        var secret = (storageMode == XeniaCredentialStorageMode.ExternalSecretReference ? externalSecretReference : apiKey)?.Trim();
        if (string.IsNullOrWhiteSpace(secret))
            throw new InvalidOperationException("A credential value or external secret reference is required.");

        var fingerprint = BuildFingerprint(secret);
        var lastFour = secret.Length <= 4 ? secret : secret[^4..];
        var record = new CredentialRecord(
            new XeniaProviderCredentialRecord(
                Guid.CreateVersion7(),
                providerConfigurationId,
                storageMode,
                fingerprint,
                lastFour,
                null,
                XeniaVerificationStatus.Unverified,
                true,
                now,
                now),
            secret);

        lock (_syncRoot)
        {
            _records[providerConfigurationId] = record;
        }

        return record.Metadata;
    }

    public XeniaProviderCredentialRecord? GetActiveMetadata(Guid providerConfigurationId)
    {
        lock (_syncRoot)
            return _records.GetValueOrDefault(providerConfigurationId)?.Metadata;
    }

    public XeniaResolvedCredential? Resolve(Guid providerConfigurationId, XeniaProviderConfiguration provider)
    {
        lock (_syncRoot)
        {
            if (_records.TryGetValue(providerConfigurationId, out var record))
                return new XeniaResolvedCredential(record.Metadata.StorageMode, record.Secret, record.Metadata.Fingerprint, record.Metadata.LastFour);
        }

        if (!string.IsNullOrWhiteSpace(provider.SecretReference))
        {
            return new XeniaResolvedCredential(
                XeniaCredentialStorageMode.ExternalSecretReference,
                provider.SecretReference,
                provider.CredentialFingerprint ?? BuildFingerprint(provider.SecretReference),
                provider.CredentialLastFour);
        }

        return null;
    }

    private static string BuildFingerprint(string rawValue)
    {
        var trimmed = rawValue.Trim();
        var suffix = trimmed.Length <= 4 ? trimmed : trimmed[^4..];
        return $"fp-{suffix}";
    }

    private sealed record CredentialRecord(XeniaProviderCredentialRecord Metadata, string Secret);
}
