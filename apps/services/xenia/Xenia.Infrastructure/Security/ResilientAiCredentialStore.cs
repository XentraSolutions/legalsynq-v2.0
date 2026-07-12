using Microsoft.Extensions.Logging;
using Xenia.Application;
using Xenia.Domain;

namespace Xenia.Infrastructure.Security;

internal sealed class ResilientAiCredentialStore(
    EncryptedDbAiCredentialStore primary,
    InMemoryAiCredentialStore fallback,
    ILogger<ResilientAiCredentialStore> logger) : IAiCredentialStore
{
    public XeniaProviderCredentialRecord Save(
        Guid providerConfigurationId,
        XeniaCredentialStorageMode storageMode,
        string? apiKey,
        string? externalSecretReference) =>
        Execute(
            () => primary.Save(providerConfigurationId, storageMode, apiKey, externalSecretReference),
            () => fallback.Save(providerConfigurationId, storageMode, apiKey, externalSecretReference));

    public XeniaProviderCredentialRecord? GetActiveMetadata(Guid providerConfigurationId) =>
        Execute(
            () => primary.GetActiveMetadata(providerConfigurationId),
            () => fallback.GetActiveMetadata(providerConfigurationId));

    public XeniaResolvedCredential? Resolve(Guid providerConfigurationId, XeniaProviderConfiguration provider) =>
        Execute(
            () => primary.Resolve(providerConfigurationId, provider),
            () => fallback.Resolve(providerConfigurationId, provider));

    private T Execute<T>(Func<T> primaryOperation, Func<T> fallbackOperation)
    {
        try
        {
            return primaryOperation();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Falling back to in-memory Xenia credential store for this request.");
            return fallbackOperation();
        }
    }
}
