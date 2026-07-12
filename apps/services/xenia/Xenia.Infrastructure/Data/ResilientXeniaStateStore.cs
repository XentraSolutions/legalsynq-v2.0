using Microsoft.Extensions.Logging;
using Xenia.Application;
using Xenia.Domain;

namespace Xenia.Infrastructure.Data;

internal sealed class ResilientXeniaStateStore(
    EfCoreXeniaStateStore primary,
    InMemoryXeniaStateStore fallback,
    ILogger<ResilientXeniaStateStore> logger) : IXeniaStateStore
{
    public IReadOnlyList<XeniaTenantConfiguration> ListTenantConfigurations() => Execute(primary.ListTenantConfigurations, fallback.ListTenantConfigurations);
    public XeniaTenantConfiguration? FindTenantConfiguration(Guid tenantId) => Execute(() => primary.FindTenantConfiguration(tenantId), () => fallback.FindTenantConfiguration(tenantId));
    public XeniaTenantConfiguration GetOrCreateTenantConfiguration(Guid tenantId) => Execute(() => primary.GetOrCreateTenantConfiguration(tenantId), () => fallback.GetOrCreateTenantConfiguration(tenantId));
    public XeniaTenantConfiguration SaveTenantConfiguration(XeniaTenantConfiguration configuration) => Execute(() => primary.SaveTenantConfiguration(configuration), () => fallback.SaveTenantConfiguration(configuration));
    public IReadOnlyList<XeniaProviderConfiguration> ListProviders() => Execute(primary.ListProviders, fallback.ListProviders);
    public XeniaProviderConfiguration? GetProvider(Guid providerConfigurationId) => Execute(() => primary.GetProvider(providerConfigurationId), () => fallback.GetProvider(providerConfigurationId));
    public XeniaProviderConfiguration SaveProvider(XeniaProviderConfiguration providerConfiguration) => Execute(() => primary.SaveProvider(providerConfiguration), () => fallback.SaveProvider(providerConfiguration));
    public IReadOnlyList<XeniaModelCatalogEntryResponse> ListModels() => Execute(primary.ListModels, fallback.ListModels);
    public IReadOnlyList<XeniaPromptTemplate> ListPromptTemplates() => Execute(primary.ListPromptTemplates, fallback.ListPromptTemplates);
    public IReadOnlyList<XeniaPromptVersion> ListPromptVersions(Guid? promptTemplateId = null) => Execute(() => primary.ListPromptVersions(promptTemplateId), () => fallback.ListPromptVersions(promptTemplateId));
    public IReadOnlyList<XeniaSkill> ListSkills() => Execute(primary.ListSkills, fallback.ListSkills);
    public IReadOnlyList<XeniaSkillVersion> ListSkillVersions(Guid? skillId = null) => Execute(() => primary.ListSkillVersions(skillId), () => fallback.ListSkillVersions(skillId));
    public IReadOnlyList<XeniaAgent> ListAgents() => Execute(primary.ListAgents, fallback.ListAgents);
    public IReadOnlyList<XeniaAgentVersion> ListAgentVersions(Guid? agentId = null) => Execute(() => primary.ListAgentVersions(agentId), () => fallback.ListAgentVersions(agentId));
    public IReadOnlyList<XeniaKnowledgeSource> ListKnowledgeSources(Guid? tenantId = null) => Execute(() => primary.ListKnowledgeSources(tenantId), () => fallback.ListKnowledgeSources(tenantId));
    public IReadOnlyList<XeniaMarketplaceAsset> ListMarketplaceAssets() => Execute(primary.ListMarketplaceAssets, fallback.ListMarketplaceAssets);
    public IReadOnlyList<XeniaMarketplaceInstallation> ListMarketplaceInstallations(Guid? tenantId = null) => Execute(() => primary.ListMarketplaceInstallations(tenantId), () => fallback.ListMarketplaceInstallations(tenantId));
    public IReadOnlyList<XeniaConversation> ListConversations(Guid tenantId) => Execute(() => primary.ListConversations(tenantId), () => fallback.ListConversations(tenantId));
    public XeniaConversation? GetConversation(Guid tenantId, Guid conversationId) => Execute(() => primary.GetConversation(tenantId, conversationId), () => fallback.GetConversation(tenantId, conversationId));
    public XeniaConversation SaveConversation(XeniaConversation conversation) => Execute(() => primary.SaveConversation(conversation), () => fallback.SaveConversation(conversation));
    public IReadOnlyList<XeniaUsageEvent> ListUsage() => Execute(primary.ListUsage, fallback.ListUsage);
    public void AppendUsage(XeniaUsageEvent usageEvent) => Execute(() => { primary.AppendUsage(usageEvent); return 0; }, () => { fallback.AppendUsage(usageEvent); return 0; });
    public IReadOnlyList<XeniaAuditEvent> ListAudit() => Execute(primary.ListAudit, fallback.ListAudit);
    public void AppendAudit(XeniaAuditEvent auditEvent) => Execute(() => { primary.AppendAudit(auditEvent); return 0; }, () => { fallback.AppendAudit(auditEvent); return 0; });
    public IReadOnlyList<XeniaProviderHealthEvent> ListProviderHealth() => Execute(primary.ListProviderHealth, fallback.ListProviderHealth);
    public void AppendProviderHealth(XeniaProviderHealthEvent providerHealthEvent) => Execute(() => { primary.AppendProviderHealth(providerHealthEvent); return 0; }, () => { fallback.AppendProviderHealth(providerHealthEvent); return 0; });

    private T Execute<T>(Func<T> primaryOperation, Func<T> fallbackOperation)
    {
        try
        {
            return primaryOperation();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Falling back to in-memory Xenia state store for this request.");
            return fallbackOperation();
        }
    }
}
