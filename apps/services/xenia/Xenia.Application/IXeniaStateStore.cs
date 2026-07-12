using Xenia.Domain;

namespace Xenia.Application;

public interface IXeniaStateStore
{
    IReadOnlyList<XeniaTenantConfiguration> ListTenantConfigurations();
    XeniaTenantConfiguration? FindTenantConfiguration(Guid tenantId);
    XeniaTenantConfiguration GetOrCreateTenantConfiguration(Guid tenantId);
    XeniaTenantConfiguration SaveTenantConfiguration(XeniaTenantConfiguration configuration);

    IReadOnlyList<XeniaProviderConfiguration> ListProviders();
    XeniaProviderConfiguration? GetProvider(Guid providerConfigurationId);
    XeniaProviderConfiguration SaveProvider(XeniaProviderConfiguration providerConfiguration);

    IReadOnlyList<XeniaModelCatalogEntryResponse> ListModels();
    IReadOnlyList<XeniaPromptTemplate> ListPromptTemplates();
    IReadOnlyList<XeniaPromptVersion> ListPromptVersions(Guid? promptTemplateId = null);
    IReadOnlyList<XeniaSkill> ListSkills();
    IReadOnlyList<XeniaSkillVersion> ListSkillVersions(Guid? skillId = null);
    IReadOnlyList<XeniaAgent> ListAgents();
    IReadOnlyList<XeniaAgentVersion> ListAgentVersions(Guid? agentId = null);
    IReadOnlyList<XeniaKnowledgeSource> ListKnowledgeSources(Guid? tenantId = null);
    IReadOnlyList<XeniaMarketplaceAsset> ListMarketplaceAssets();
    IReadOnlyList<XeniaMarketplaceInstallation> ListMarketplaceInstallations(Guid? tenantId = null);

    IReadOnlyList<XeniaConversation> ListConversations(Guid tenantId);
    XeniaConversation? GetConversation(Guid tenantId, Guid conversationId);
    XeniaConversation SaveConversation(XeniaConversation conversation);

    IReadOnlyList<XeniaUsageEvent> ListUsage();
    void AppendUsage(XeniaUsageEvent usageEvent);

    IReadOnlyList<XeniaAuditEvent> ListAudit();
    void AppendAudit(XeniaAuditEvent auditEvent);

    IReadOnlyList<XeniaProviderHealthEvent> ListProviderHealth();
    void AppendProviderHealth(XeniaProviderHealthEvent providerHealthEvent);
}
