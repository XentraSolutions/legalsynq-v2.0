namespace Xenia.Application;

public interface IXeniaService
{
    XeniaAdminOverviewResponse GetAdminOverview();
    XeniaTenantConfigurationResponse GetManagedConfiguration();
    XeniaTenantConfigurationResponse SaveManagedConfiguration(XeniaTenantConfigurationRequest request, string actorUserId);
    XeniaTenantConfigurationResponse GetTenantConfiguration(Guid tenantId);
    IReadOnlyList<XeniaProviderConfigurationResponse> ListProviders(Guid? tenantId = null);
    XeniaProviderConfigurationResponse CreatePlatformProvider(XeniaProviderConfigurationRequest request, string actorUserId);
    XeniaProviderConfigurationResponse UpdatePlatformProvider(Guid providerConfigurationId, XeniaProviderConfigurationRequest request, string actorUserId);
    XeniaProviderTestResponse TestProvider(Guid? tenantId, Guid? providerConfigurationId, XeniaProviderConfigurationRequest? request, string actorUserId);
    IReadOnlyList<XeniaModelCatalogEntryResponse> ListModels();
    IReadOnlyList<XeniaPromptTemplateResponse> ListPromptTemplates();
    IReadOnlyList<XeniaPromptVersionResponse> ListPromptVersions(Guid? promptTemplateId = null);
    IReadOnlyList<XeniaSkillResponse> ListSkills();
    IReadOnlyList<XeniaSkillVersionResponse> ListSkillVersions(Guid? skillId = null);
    IReadOnlyList<XeniaAgentResponse> ListAgents();
    IReadOnlyList<XeniaAgentVersionResponse> ListAgentVersions(Guid? agentId = null);
    IReadOnlyList<XeniaKnowledgeSourceResponse> ListKnowledgeSources(Guid? tenantId = null);
    IReadOnlyList<XeniaMarketplaceAssetResponse> ListMarketplaceAssets();
    IReadOnlyList<XeniaMarketplaceInstallationResponse> ListMarketplaceInstallations(Guid? tenantId = null);
    XeniaUsageReportResponse GetUsage(Guid? tenantId = null);
    IReadOnlyList<XeniaAuditEventResponse> GetAudit(Guid? tenantId = null);
    IReadOnlyList<XeniaProviderHealthResponse> GetProviderHealth();
    XeniaTenantConfigurationResponse SaveTenantByoAiConfiguration(Guid tenantId, XeniaProviderConfigurationRequest request, string actorUserId);
    XeniaConversationResponse CreateConversation(Guid tenantId, string userId, XeniaCreateConversationRequest request);
    IReadOnlyList<XeniaConversationResponse> ListConversations(Guid tenantId);
    XeniaConversationResponse GetConversation(Guid tenantId, Guid conversationId);
    XeniaConversationTurnResponse AddConversationMessage(Guid tenantId, Guid conversationId, string userId, XeniaConversationMessageRequest request);
    XeniaExecutionResponse ExecuteInternal(Guid tenantId, string userId, XeniaExecutionRequest request, string mode, string? skillCode = null, string? agentCode = null, string? toolCode = null);
}
