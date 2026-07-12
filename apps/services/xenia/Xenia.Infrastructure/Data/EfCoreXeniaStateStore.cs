using Microsoft.EntityFrameworkCore;
using Xenia.Application;
using Xenia.Domain;

namespace Xenia.Infrastructure.Data;

internal sealed class EfCoreXeniaStateStore(XeniaDbContext dbContext) : IXeniaStateStore
{
    public IReadOnlyList<XeniaTenantConfiguration> ListTenantConfigurations() =>
        dbContext.TenantConfigurations
            .AsNoTracking()
            .OrderBy(item => item.TenantId)
            .ToList();

    public XeniaTenantConfiguration? FindTenantConfiguration(Guid tenantId) =>
        dbContext.TenantConfigurations.SingleOrDefault(item => item.TenantId == tenantId);

    public XeniaTenantConfiguration GetOrCreateTenantConfiguration(Guid tenantId)
    {
        var configuration = FindTenantConfiguration(tenantId);
        if (configuration is not null)
            return configuration;

        var defaultProvider = dbContext.ProviderConfigurations
            .OrderBy(item => item.FailoverPriority)
            .FirstOrDefault(item => item.Scope == XeniaProviderScope.Platform && item.Enabled);

        configuration = new XeniaTenantConfiguration
        {
            TenantId = tenantId,
            DefaultProviderConfigurationId = defaultProvider?.ProviderConfigurationId,
            DefaultModel = defaultProvider?.DefaultModel ?? "gpt-4.1-mini",
            AllowedSkills = ["summary", "analysis", "drafting"],
            AllowedAgents = ["workspace-assistant"],
            AllowedTools = ["document-search", "timeline-builder"],
        };

        dbContext.TenantConfigurations.Add(configuration);
        dbContext.SaveChanges();
        return configuration;
    }

    public XeniaTenantConfiguration SaveTenantConfiguration(XeniaTenantConfiguration configuration)
    {
        var existing = dbContext.TenantConfigurations.SingleOrDefault(item => item.TenantId == configuration.TenantId);
        if (existing is null)
        {
            dbContext.TenantConfigurations.Add(configuration);
        }
        else
        {
            dbContext.Entry(existing).CurrentValues.SetValues(configuration);
        }

        dbContext.SaveChanges();
        return dbContext.TenantConfigurations.Single(item => item.TenantId == configuration.TenantId);
    }

    public IReadOnlyList<XeniaProviderConfiguration> ListProviders() =>
        dbContext.ProviderConfigurations
            .AsNoTracking()
            .OrderBy(item => item.Scope)
            .ThenBy(item => item.FailoverPriority)
            .ThenBy(item => item.DisplayName)
            .ToList();

    public XeniaProviderConfiguration? GetProvider(Guid providerConfigurationId) =>
        dbContext.ProviderConfigurations.SingleOrDefault(item => item.ProviderConfigurationId == providerConfigurationId);

    public XeniaProviderConfiguration SaveProvider(XeniaProviderConfiguration providerConfiguration)
    {
        var existing = dbContext.ProviderConfigurations.SingleOrDefault(item => item.ProviderConfigurationId == providerConfiguration.ProviderConfigurationId);
        if (existing is null)
        {
            dbContext.ProviderConfigurations.Add(providerConfiguration);
        }
        else
        {
            dbContext.Entry(existing).CurrentValues.SetValues(providerConfiguration);
        }

        dbContext.SaveChanges();
        return dbContext.ProviderConfigurations.Single(item => item.ProviderConfigurationId == providerConfiguration.ProviderConfigurationId);
    }

    public IReadOnlyList<XeniaModelCatalogEntryResponse> ListModels() =>
        dbContext.ModelCatalogEntries
            .AsNoTracking()
            .OrderBy(item => item.Provider)
            .ThenBy(item => item.ModelCode)
            .Select(item => new XeniaModelCatalogEntryResponse(
                item.Provider,
                item.ModelCode,
                item.DisplayName,
                item.SupportsStreaming,
                item.SupportsEmbeddings,
                item.Enabled))
            .ToList();

    public IReadOnlyList<XeniaPromptTemplate> ListPromptTemplates() =>
        dbContext.PromptTemplates.AsNoTracking().ToList();

    public IReadOnlyList<XeniaPromptVersion> ListPromptVersions(Guid? promptTemplateId = null) =>
        dbContext.PromptVersions
            .AsNoTracking()
            .Where(item => !promptTemplateId.HasValue || item.PromptTemplateId == promptTemplateId)
            .ToList();

    public IReadOnlyList<XeniaSkill> ListSkills() =>
        dbContext.Skills.AsNoTracking().ToList();

    public IReadOnlyList<XeniaSkillVersion> ListSkillVersions(Guid? skillId = null) =>
        dbContext.SkillVersions
            .AsNoTracking()
            .Where(item => !skillId.HasValue || item.SkillId == skillId)
            .ToList();

    public IReadOnlyList<XeniaAgent> ListAgents() =>
        dbContext.Agents.AsNoTracking().ToList();

    public IReadOnlyList<XeniaAgentVersion> ListAgentVersions(Guid? agentId = null) =>
        dbContext.AgentVersions
            .AsNoTracking()
            .Where(item => !agentId.HasValue || item.AgentId == agentId)
            .ToList();

    public IReadOnlyList<XeniaKnowledgeSource> ListKnowledgeSources(Guid? tenantId = null) =>
        dbContext.KnowledgeSources
            .AsNoTracking()
            .Where(item => !tenantId.HasValue || item.TenantId == null || item.TenantId == tenantId)
            .ToList();

    public IReadOnlyList<XeniaMarketplaceAsset> ListMarketplaceAssets() =>
        dbContext.MarketplaceAssets.AsNoTracking().ToList();

    public IReadOnlyList<XeniaMarketplaceInstallation> ListMarketplaceInstallations(Guid? tenantId = null) =>
        dbContext.MarketplaceInstallations
            .AsNoTracking()
            .Where(item => !tenantId.HasValue || item.TenantId == tenantId)
            .ToList();

    public IReadOnlyList<XeniaConversation> ListConversations(Guid tenantId)
    {
        var conversations = dbContext.Conversations
            .AsNoTracking()
            .Where(item => item.TenantId == tenantId)
            .OrderByDescending(item => item.UpdatedAtUtc)
            .ToList();

        var conversationIds = conversations.Select(item => item.ConversationId).ToList();
        if (conversationIds.Count == 0)
            return conversations;

        var messages = dbContext.ConversationMessages
            .AsNoTracking()
            .Where(item => conversationIds.Contains(item.ConversationId))
            .OrderBy(item => item.CreatedAtUtc)
            .ToList()
            .GroupBy(item => item.ConversationId)
            .ToDictionary(group => group.Key, group => group.ToList());

        foreach (var conversation in conversations)
            conversation.Messages = messages.GetValueOrDefault(conversation.ConversationId, []);

        return conversations;
    }

    public XeniaConversation? GetConversation(Guid tenantId, Guid conversationId)
    {
        var conversation = dbContext.Conversations
            .AsNoTracking()
            .SingleOrDefault(item => item.TenantId == tenantId && item.ConversationId == conversationId);

        if (conversation is null)
            return null;

        conversation.Messages = dbContext.ConversationMessages
            .AsNoTracking()
            .Where(item => item.ConversationId == conversationId)
            .OrderBy(item => item.CreatedAtUtc)
            .ToList();

        return conversation;
    }

    public XeniaConversation SaveConversation(XeniaConversation conversation)
    {
        var existing = dbContext.Conversations.SingleOrDefault(item => item.ConversationId == conversation.ConversationId);
        if (existing is null)
        {
            dbContext.Conversations.Add(new XeniaConversation
            {
                ConversationId = conversation.ConversationId,
                TenantId = conversation.TenantId,
                CreatedByUserId = conversation.CreatedByUserId,
                Title = conversation.Title,
                ActivationSource = conversation.ActivationSource,
                ProductCode = conversation.ProductCode,
                SourceReference = conversation.SourceReference,
                CreatedAtUtc = conversation.CreatedAtUtc,
                UpdatedAtUtc = conversation.UpdatedAtUtc,
            });
        }
        else
        {
            dbContext.Entry(existing).CurrentValues.SetValues(new
            {
                conversation.TenantId,
                conversation.CreatedByUserId,
                conversation.Title,
                conversation.ActivationSource,
                conversation.ProductCode,
                conversation.SourceReference,
                conversation.CreatedAtUtc,
                conversation.UpdatedAtUtc,
            });
        }

        var existingMessages = dbContext.ConversationMessages
            .Where(item => item.ConversationId == conversation.ConversationId)
            .Select(item => item.MessageId)
            .ToHashSet();

        foreach (var message in conversation.Messages.Where(message => !existingMessages.Contains(message.MessageId)))
            dbContext.ConversationMessages.Add(message);

        dbContext.SaveChanges();
        return GetConversation(conversation.TenantId, conversation.ConversationId)!;
    }

    public IReadOnlyList<XeniaUsageEvent> ListUsage() =>
        dbContext.UsageLedger.AsNoTracking().OrderByDescending(item => item.CreatedAtUtc).ToList();

    public void AppendUsage(XeniaUsageEvent usageEvent)
    {
        dbContext.UsageLedger.Add(usageEvent);
        dbContext.CostLedger.Add(new XeniaCostLedgerEntry
        {
            CostLedgerEntryId = Guid.CreateVersion7(),
            TenantId = usageEvent.TenantId,
            UsageEventId = usageEvent.UsageEventId,
            ProductCode = usageEvent.EventKind,
            Provider = usageEvent.Provider,
            Model = usageEvent.Model,
            EstimatedCostUsd = usageEvent.EstimatedCostUsd,
            CreatedAtUtc = usageEvent.CreatedAtUtc,
        });
        dbContext.SaveChanges();
    }

    public IReadOnlyList<XeniaAuditEvent> ListAudit() =>
        dbContext.AuditEvents.AsNoTracking().OrderByDescending(item => item.CreatedAtUtc).ToList();

    public void AppendAudit(XeniaAuditEvent auditEvent)
    {
        dbContext.AuditEvents.Add(auditEvent);
        dbContext.SaveChanges();
    }

    public IReadOnlyList<XeniaProviderHealthEvent> ListProviderHealth() =>
        dbContext.ProviderHealthEvents.AsNoTracking().OrderByDescending(item => item.CheckedAtUtc).ToList();

    public void AppendProviderHealth(XeniaProviderHealthEvent providerHealthEvent)
    {
        dbContext.ProviderHealthEvents.Add(providerHealthEvent);
        dbContext.SaveChanges();
    }
}
