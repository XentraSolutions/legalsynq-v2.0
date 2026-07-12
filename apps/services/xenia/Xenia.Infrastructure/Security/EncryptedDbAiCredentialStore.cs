using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Xenia.Application;
using Xenia.Domain;
using Xenia.Infrastructure.Data;

namespace Xenia.Infrastructure.Security;

internal sealed class EncryptedDbAiCredentialStore(
    XeniaDbContext dbContext,
    IDataProtectionProvider dataProtectionProvider) : IAiCredentialStore
{
    private readonly IDataProtector _protector = dataProtectionProvider.CreateProtector("Xenia.ProviderCredentials.v1");

    public XeniaProviderCredentialRecord Save(
        Guid providerConfigurationId,
        XeniaCredentialStorageMode storageMode,
        string? apiKey,
        string? externalSecretReference)
    {
        var now = DateTime.UtcNow;

        var existingActive = dbContext.ProviderCredentials
            .Where(item => item.ProviderConfigurationId == providerConfigurationId && item.IsActive)
            .ToList();

        foreach (var credential in existingActive)
        {
            credential.IsActive = false;
            credential.RotatedAtUtc = now;
        }

        var trimmedApiKey = apiKey?.Trim();
        var trimmedSecretReference = externalSecretReference?.Trim();
        var rawSecret = storageMode == XeniaCredentialStorageMode.ExternalSecretReference ? trimmedSecretReference : trimmedApiKey;

        if (string.IsNullOrWhiteSpace(rawSecret))
            throw new InvalidOperationException("A credential value or external secret reference is required.");

        var fingerprint = BuildFingerprint(rawSecret);
        var lastFour = rawSecret.Length <= 4 ? rawSecret : rawSecret[^4..];

        var credentialRecord = new XeniaProviderCredential
        {
            ProviderCredentialId = Guid.CreateVersion7(),
            ProviderConfigurationId = providerConfigurationId,
            StorageMode = storageMode,
            EncryptedSecretPayload = storageMode == XeniaCredentialStorageMode.EncryptedDatabase ? _protector.Protect(rawSecret) : null,
            ExternalSecretReference = storageMode == XeniaCredentialStorageMode.ExternalSecretReference ? trimmedSecretReference : null,
            Fingerprint = fingerprint,
            LastFour = lastFour,
            IsActive = true,
            VerificationStatus = XeniaVerificationStatus.Unverified,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };

        dbContext.ProviderCredentials.Add(credentialRecord);
        dbContext.SaveChanges();

        return Map(credentialRecord);
    }

    public XeniaProviderCredentialRecord? GetActiveMetadata(Guid providerConfigurationId)
    {
        var credential = dbContext.ProviderCredentials
            .AsNoTracking()
            .OrderByDescending(item => item.CreatedAtUtc)
            .FirstOrDefault(item => item.ProviderConfigurationId == providerConfigurationId && item.IsActive);

        return credential is null ? null : Map(credential);
    }

    public XeniaResolvedCredential? Resolve(Guid providerConfigurationId, XeniaProviderConfiguration provider)
    {
        var credential = dbContext.ProviderCredentials
            .AsNoTracking()
            .OrderByDescending(item => item.CreatedAtUtc)
            .FirstOrDefault(item => item.ProviderConfigurationId == providerConfigurationId && item.IsActive);

        if (credential is null)
        {
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

        var secret = credential.StorageMode == XeniaCredentialStorageMode.ExternalSecretReference
            ? credential.ExternalSecretReference
            : _protector.Unprotect(credential.EncryptedSecretPayload ?? string.Empty);

        if (string.IsNullOrWhiteSpace(secret))
            return null;

        return new XeniaResolvedCredential(credential.StorageMode, secret, credential.Fingerprint, credential.LastFour);
    }

    private static string BuildFingerprint(string rawValue)
    {
        var trimmed = rawValue.Trim();
        var suffix = trimmed.Length <= 4 ? trimmed : trimmed[^4..];
        return $"fp-{suffix}";
    }

    private static XeniaProviderCredentialRecord Map(XeniaProviderCredential credential) =>
        new(
            credential.ProviderCredentialId,
            credential.ProviderConfigurationId,
            credential.StorageMode,
            credential.Fingerprint,
            credential.LastFour,
            credential.LastVerifiedAtUtc,
            credential.VerificationStatus,
            credential.IsActive,
            credential.CreatedAtUtc,
            credential.UpdatedAtUtc);
}
