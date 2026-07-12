using Xenia.Application;
using Xenia.Domain;

namespace Xenia.Infrastructure.Providers;

internal sealed class DefaultProviderRoutingPolicy : IProviderRoutingPolicy
{
    public XeniaProviderConfiguration Resolve(XeniaTenantConfiguration configuration, Guid tenantId, IReadOnlyList<XeniaProviderConfiguration> providers)
    {
        if (configuration.DefaultProviderConfigurationId.HasValue)
        {
            var explicitlyConfigured = providers.FirstOrDefault(item =>
                item.ProviderConfigurationId == configuration.DefaultProviderConfigurationId.Value && item.Enabled);
            if (explicitlyConfigured is not null)
                return explicitlyConfigured;
        }

        if (configuration.DeploymentModel == XeniaDeploymentModel.BringYourOwnAI)
        {
            var tenantProvider = providers
                .Where(item => item.Scope == XeniaProviderScope.Tenant && item.TenantId == tenantId && item.Enabled)
                .OrderBy(item => item.FailoverPriority)
                .FirstOrDefault();

            if (tenantProvider is not null)
                return tenantProvider;
        }

        return providers
            .Where(item => item.Scope == XeniaProviderScope.Platform && item.Enabled)
            .OrderBy(item => item.FailoverPriority)
            .First();
    }
}

internal sealed class DefaultProviderFailoverPolicy : IProviderFailoverPolicy
{
    public XeniaProviderConfiguration SelectFallback(
        XeniaTenantConfiguration configuration,
        Guid tenantId,
        IReadOnlyList<XeniaProviderConfiguration> providers,
        Guid failedProviderConfigurationId)
    {
        var scopedProviders = configuration.DeploymentModel == XeniaDeploymentModel.BringYourOwnAI
            ? providers.Where(item => item.Scope == XeniaProviderScope.Tenant && item.TenantId == tenantId && item.Enabled)
            : providers.Where(item => item.Scope == XeniaProviderScope.Platform && item.Enabled);

        return scopedProviders
            .OrderBy(item => item.FailoverPriority)
            .First(item => item.ProviderConfigurationId != failedProviderConfigurationId);
    }
}

internal sealed class XeniaUsageNormalizer : IAiUsageNormalizer
{
    public XeniaUsageEvent CreateUsageEvent(Guid tenantId, string userId, string eventKind, XeniaAiResponse response) =>
        new()
        {
            UsageEventId = Guid.CreateVersion7(),
            TenantId = tenantId,
            UserId = userId,
            EventKind = eventKind,
            Provider = response.Provider,
            Model = response.Model,
            PromptTokens = response.PromptTokens,
            CompletionTokens = response.CompletionTokens,
            EstimatedCostUsd = response.EstimatedCostUsd,
            CreatedAtUtc = DateTime.UtcNow,
        };
}

internal sealed class XeniaProviderHealthCheck : IAiProviderHealthCheck
{
    public XeniaProviderHealthEvent CreateHealthEvent(XeniaProviderConfiguration provider, XeniaProviderValidationResult result) =>
        new()
        {
            ProviderHealthEventId = Guid.CreateVersion7(),
            ProviderConfigurationId = provider.ProviderConfigurationId,
            ProviderName = provider.DisplayName,
            Status = result.Success ? "Healthy" : "Degraded",
            Message = result.Message,
            CheckedAtUtc = result.VerifiedAtUtc,
        };
}
