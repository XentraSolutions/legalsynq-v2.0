using Xenia.Application;
using Xenia.Domain;

namespace Xenia.Infrastructure.Providers;

internal sealed class AiProviderGateway(
    IEnumerable<IAiProviderAdapter> adapters,
    IAiCredentialStore credentialStore) : IAiProviderGateway
{
    public XeniaAiResponse Execute(XeniaProviderConfiguration provider, XeniaAiExecutionContext context)
    {
        var credential = credentialStore.Resolve(provider.ProviderConfigurationId, provider);
        if (credential is null)
            throw new InvalidOperationException($"Provider '{provider.DisplayName}' does not have an active credential.");

        var adapter = adapters.FirstOrDefault(candidate => candidate.CanHandle(provider.ProviderType))
            ?? throw new InvalidOperationException($"No AI provider adapter is registered for '{provider.ProviderType}'.");

        return adapter.Execute(provider, credential, context);
    }

    public XeniaProviderValidationResult Validate(XeniaProviderConfiguration provider, XeniaResolvedCredential credential)
    {
        var adapter = adapters.FirstOrDefault(candidate => candidate.CanHandle(provider.ProviderType))
            ?? throw new InvalidOperationException($"No AI provider adapter is registered for '{provider.ProviderType}'.");

        return adapter.Validate(provider, credential);
    }
}
