using Xenia.Application;
using Xenia.Domain;

namespace Xenia.Infrastructure.Data;

internal sealed class InMemoryXeniaStateStore : IXeniaStateStore
{
    private readonly Lock _syncRoot = new();
    private readonly Dictionary<Guid, XeniaTenantConfiguration> _tenantConfigurations = [];
    private readonly Dictionary<Guid, XeniaProviderConfiguration> _providers = [];
    private readonly Dictionary<Guid, XeniaConversation> _conversations = [];
    private readonly List<XeniaPromptTemplate> _promptTemplates = [];
    private readonly List<XeniaPromptVersion> _promptVersions = [];
    private readonly List<XeniaSkill> _skills = [];
    private readonly List<XeniaSkillVersion> _skillVersions = [];
    private readonly List<XeniaAgent> _agents = [];
    private readonly List<XeniaAgentVersion> _agentVersions = [];
    private readonly List<XeniaKnowledgeSource> _knowledgeSources = [];
    private readonly List<XeniaMarketplaceAsset> _marketplaceAssets = [];
    private readonly List<XeniaMarketplaceInstallation> _marketplaceInstallations = [];
    private readonly List<XeniaUsageEvent> _usageEvents = [];
    private readonly List<XeniaAuditEvent> _auditEvents = [];
    private readonly List<XeniaProviderHealthEvent> _providerHealthEvents = [];
    private readonly List<XeniaModelCatalogEntryResponse> _models = [];

    public InMemoryXeniaStateStore()
    {
        Seed();
    }

    public IReadOnlyList<XeniaTenantConfiguration> ListTenantConfigurations()
    {
        lock (_syncRoot)
            return _tenantConfigurations.Values.Select(Clone).ToList();
    }

    public XeniaTenantConfiguration? FindTenantConfiguration(Guid tenantId)
    {
        lock (_syncRoot)
            return _tenantConfigurations.TryGetValue(tenantId, out var configuration) ? Clone(configuration) : null;
    }

    public XeniaTenantConfiguration GetOrCreateTenantConfiguration(Guid tenantId)
    {
        lock (_syncRoot)
        {
            if (_tenantConfigurations.TryGetValue(tenantId, out var existing))
                return Clone(existing);

            var provider = _providers.Values
                .Where(item => item.Scope == XeniaProviderScope.Platform && item.Enabled)
                .OrderBy(item => item.FailoverPriority)
                .FirstOrDefault();

            var created = new XeniaTenantConfiguration
            {
                TenantId = tenantId,
                DefaultProviderConfigurationId = provider?.ProviderConfigurationId,
                DefaultModel = provider?.DefaultModel ?? "gpt-4.1-mini",
                AllowedSkills = ["summary"],
                AllowedAgents = ["workspace-assistant"],
                AllowedTools = ["document-search"],
            };

            _tenantConfigurations[tenantId] = Clone(created);
            return created;
        }
    }

    public XeniaTenantConfiguration SaveTenantConfiguration(XeniaTenantConfiguration configuration)
    {
        lock (_syncRoot)
        {
            var saved = Clone(configuration);
            _tenantConfigurations[configuration.TenantId] = saved;
            return Clone(saved);
        }
    }

    public IReadOnlyList<XeniaProviderConfiguration> ListProviders()
    {
        lock (_syncRoot)
            return _providers.Values
                .OrderBy(item => item.Scope)
                .ThenBy(item => item.FailoverPriority)
                .ThenBy(item => item.DisplayName)
                .Select(Clone)
                .ToList();
    }

    public XeniaProviderConfiguration? GetProvider(Guid providerConfigurationId)
    {
        lock (_syncRoot)
            return _providers.TryGetValue(providerConfigurationId, out var provider) ? Clone(provider) : null;
    }

    public XeniaProviderConfiguration SaveProvider(XeniaProviderConfiguration providerConfiguration)
    {
        lock (_syncRoot)
        {
            var saved = Clone(providerConfiguration);
            _providers[providerConfiguration.ProviderConfigurationId] = saved;
            return Clone(saved);
        }
    }

    public IReadOnlyList<XeniaModelCatalogEntryResponse> ListModels()
    {
        lock (_syncRoot)
            return _models.ToList();
    }

    public IReadOnlyList<XeniaPromptTemplate> ListPromptTemplates()
    {
        lock (_syncRoot)
            return _promptTemplates.Select(Clone).ToList();
    }

    public IReadOnlyList<XeniaPromptVersion> ListPromptVersions(Guid? promptTemplateId = null)
    {
        lock (_syncRoot)
            return _promptVersions
                .Where(item => !promptTemplateId.HasValue || item.PromptTemplateId == promptTemplateId)
                .Select(Clone)
                .ToList();
    }

    public IReadOnlyList<XeniaSkill> ListSkills()
    {
        lock (_syncRoot)
            return _skills.Select(Clone).ToList();
    }

    public IReadOnlyList<XeniaSkillVersion> ListSkillVersions(Guid? skillId = null)
    {
        lock (_syncRoot)
            return _skillVersions
                .Where(item => !skillId.HasValue || item.SkillId == skillId)
                .Select(Clone)
                .ToList();
    }

    public IReadOnlyList<XeniaAgent> ListAgents()
    {
        lock (_syncRoot)
            return _agents.Select(Clone).ToList();
    }

    public IReadOnlyList<XeniaAgentVersion> ListAgentVersions(Guid? agentId = null)
    {
        lock (_syncRoot)
            return _agentVersions
                .Where(item => !agentId.HasValue || item.AgentId == agentId)
                .Select(Clone)
                .ToList();
    }

    public IReadOnlyList<XeniaKnowledgeSource> ListKnowledgeSources(Guid? tenantId = null)
    {
        lock (_syncRoot)
            return _knowledgeSources
                .Where(item => !tenantId.HasValue || item.TenantId == null || item.TenantId == tenantId)
                .Select(Clone)
                .ToList();
    }

    public IReadOnlyList<XeniaMarketplaceAsset> ListMarketplaceAssets()
    {
        lock (_syncRoot)
            return _marketplaceAssets.Select(Clone).ToList();
    }

    public IReadOnlyList<XeniaMarketplaceInstallation> ListMarketplaceInstallations(Guid? tenantId = null)
    {
        lock (_syncRoot)
            return _marketplaceInstallations
                .Where(item => !tenantId.HasValue || item.TenantId == tenantId)
                .Select(Clone)
                .ToList();
    }

    public IReadOnlyList<XeniaConversation> ListConversations(Guid tenantId)
    {
        lock (_syncRoot)
            return _conversations.Values
                .Where(item => item.TenantId == tenantId)
                .OrderByDescending(item => item.UpdatedAtUtc)
                .Select(Clone)
                .ToList();
    }

    public XeniaConversation? GetConversation(Guid tenantId, Guid conversationId)
    {
        lock (_syncRoot)
        {
            if (!_conversations.TryGetValue(conversationId, out var conversation) || conversation.TenantId != tenantId)
                return null;

            return Clone(conversation);
        }
    }

    public XeniaConversation SaveConversation(XeniaConversation conversation)
    {
        lock (_syncRoot)
        {
            var saved = Clone(conversation);
            _conversations[conversation.ConversationId] = saved;
            return Clone(saved);
        }
    }

    public IReadOnlyList<XeniaUsageEvent> ListUsage()
    {
        lock (_syncRoot)
            return _usageEvents.OrderByDescending(item => item.CreatedAtUtc).Select(Clone).ToList();
    }

    public void AppendUsage(XeniaUsageEvent usageEvent)
    {
        lock (_syncRoot)
            _usageEvents.Add(Clone(usageEvent));
    }

    public IReadOnlyList<XeniaAuditEvent> ListAudit()
    {
        lock (_syncRoot)
            return _auditEvents.OrderByDescending(item => item.CreatedAtUtc).Select(Clone).ToList();
    }

    public void AppendAudit(XeniaAuditEvent auditEvent)
    {
        lock (_syncRoot)
            _auditEvents.Add(Clone(auditEvent));
    }

    public IReadOnlyList<XeniaProviderHealthEvent> ListProviderHealth()
    {
        lock (_syncRoot)
            return _providerHealthEvents.OrderByDescending(item => item.CheckedAtUtc).Select(Clone).ToList();
    }

    public void AppendProviderHealth(XeniaProviderHealthEvent providerHealthEvent)
    {
        lock (_syncRoot)
            _providerHealthEvents.Add(Clone(providerHealthEvent));
    }

    private void Seed()
    {
        var seededAt = new DateTime(2026, 7, 9, 0, 0, 0, DateTimeKind.Utc);
        var platformProviderId = Guid.Parse("10000000-0000-0000-0000-000000003201");
        var promptTemplateId = Guid.Parse("10000000-0000-0000-0000-000000003401");
        var skillId = Guid.Parse("10000000-0000-0000-0000-000000003411");
        var agentId = Guid.Parse("10000000-0000-0000-0000-000000003421");

        _providers[platformProviderId] = new XeniaProviderConfiguration
        {
            ProviderConfigurationId = platformProviderId,
            ProviderType = XeniaProviderType.OpenAI,
            Scope = XeniaProviderScope.Platform,
            DisplayName = "Managed OpenAI",
            DefaultModel = "gpt-4.1-mini",
            AllowedModels = ["gpt-4.1-mini", "gpt-4.1"],
            FailoverPriority = 10,
            Enabled = true,
            CredentialStorageMode = XeniaCredentialStorageMode.ExternalSecretReference,
            SecretReference = "platform://xenia/openai",
            CredentialFingerprint = "fp-nai",
            CredentialLastFour = "nai",
            HasStoredCredential = true,
            VerificationStatus = XeniaVerificationStatus.Unverified,
            CreatedAtUtc = seededAt,
            UpdatedAtUtc = seededAt,
        };

        _models.Add(new XeniaModelCatalogEntryResponse("OpenAI", "gpt-4.1-mini", "GPT-4.1 Mini", true, true, true));
        _models.Add(new XeniaModelCatalogEntryResponse("Anthropic", "claude-3-7-sonnet", "Claude 3.7 Sonnet", true, false, true));
        _models.Add(new XeniaModelCatalogEntryResponse("Gemini", "gemini-2.5-pro", "Gemini 2.5 Pro", true, true, true));

        _promptTemplates.Add(new XeniaPromptTemplate
        {
            PromptTemplateId = promptTemplateId,
            TemplateCode = "general-summary",
            DisplayName = "General Summary",
            Description = "Default summary prompt template for cross-product Xenia work.",
            Enabled = true,
            CreatedAtUtc = seededAt,
            UpdatedAtUtc = seededAt,
        });

        _promptVersions.Add(new XeniaPromptVersion
        {
            PromptVersionId = Guid.Parse("10000000-0000-0000-0000-000000003402"),
            PromptTemplateId = promptTemplateId,
            VersionNumber = 1,
            Content = "Summarize the supplied context with factual accuracy, explicit caveats, and actionable next steps.",
            ApprovalState = "Approved",
            IsCurrent = true,
            CreatedAtUtc = seededAt,
            UpdatedAtUtc = seededAt,
        });

        _skills.Add(new XeniaSkill
        {
            SkillId = skillId,
            SkillCode = "summary",
            DisplayName = "Summary",
            Description = "Summarize documents, cases, or workflow state into a concise operational brief.",
            Enabled = true,
            CreatedAtUtc = seededAt,
            UpdatedAtUtc = seededAt,
        });

        _skillVersions.Add(new XeniaSkillVersion
        {
            SkillVersionId = Guid.Parse("10000000-0000-0000-0000-000000003412"),
            SkillId = skillId,
            VersionNumber = 1,
            DefinitionJson = "{\"capabilities\":[\"summary\",\"briefing\"]}",
            ApprovalState = "Approved",
            IsCurrent = true,
            CreatedAtUtc = seededAt,
            UpdatedAtUtc = seededAt,
        });

        _agents.Add(new XeniaAgent
        {
            AgentId = agentId,
            AgentCode = "workspace-assistant",
            DisplayName = "Workspace Assistant",
            Description = "Default tenant-facing Xenia assistant for summaries, drafts, and operational reasoning.",
            Enabled = true,
            CreatedAtUtc = seededAt,
            UpdatedAtUtc = seededAt,
        });

        _agentVersions.Add(new XeniaAgentVersion
        {
            AgentVersionId = Guid.Parse("10000000-0000-0000-0000-000000003422"),
            AgentId = agentId,
            VersionNumber = 1,
            DefinitionJson = "{\"style\":\"operational\",\"approvalRequired\":true}",
            ApprovalState = "Approved",
            IsCurrent = true,
            CreatedAtUtc = seededAt,
            UpdatedAtUtc = seededAt,
        });

        _knowledgeSources.Add(new XeniaKnowledgeSource
        {
            KnowledgeSourceId = Guid.Parse("10000000-0000-0000-0000-000000003431"),
            SourceCode = "platform-docs",
            DisplayName = "Platform Documentation",
            SourceType = "DocumentSet",
            Status = "Ready",
            CreatedAtUtc = seededAt,
            UpdatedAtUtc = seededAt,
        });

        _marketplaceAssets.Add(new XeniaMarketplaceAsset
        {
            MarketplaceAssetId = Guid.Parse("10000000-0000-0000-0000-000000003451"),
            AssetCode = "legal-summary-pack",
            AssetType = "PromptPack",
            DisplayName = "Legal Summary Pack",
            Description = "Baseline summary prompts and policies for legal operations.",
            Enabled = true,
            CreatedAtUtc = seededAt,
            UpdatedAtUtc = seededAt,
        });
    }

    private static XeniaTenantConfiguration Clone(XeniaTenantConfiguration value) => new()
    {
        TenantId = value.TenantId,
        Enabled = value.Enabled,
        DeploymentModel = value.DeploymentModel,
        DefaultProviderConfigurationId = value.DefaultProviderConfigurationId,
        DefaultModel = value.DefaultModel,
        Temperature = value.Temperature,
        MaxTokens = value.MaxTokens,
        ReasoningLevel = value.ReasoningLevel,
        RetentionPolicy = value.RetentionPolicy,
        ModerationPolicy = value.ModerationPolicy,
        FailoverEnabled = value.FailoverEnabled,
        BudgetPolicyId = value.BudgetPolicyId,
        QuotaPolicyId = value.QuotaPolicyId,
        AllowedSkills = [.. value.AllowedSkills],
        AllowedAgents = [.. value.AllowedAgents],
        AllowedTools = [.. value.AllowedTools],
        CreatedAtUtc = value.CreatedAtUtc,
        UpdatedAtUtc = value.UpdatedAtUtc,
    };

    private static XeniaProviderConfiguration Clone(XeniaProviderConfiguration value) => new()
    {
        ProviderConfigurationId = value.ProviderConfigurationId,
        ProviderType = value.ProviderType,
        Scope = value.Scope,
        TenantId = value.TenantId,
        DisplayName = value.DisplayName,
        Endpoint = value.Endpoint,
        Region = value.Region,
        AzureDeploymentName = value.AzureDeploymentName,
        DefaultModel = value.DefaultModel,
        AllowedModels = [.. value.AllowedModels],
        TimeoutSeconds = value.TimeoutSeconds,
        RetryCount = value.RetryCount,
        FailoverPriority = value.FailoverPriority,
        Enabled = value.Enabled,
        VerificationStatus = value.VerificationStatus,
        LastVerifiedAtUtc = value.LastVerifiedAtUtc,
        CredentialStorageMode = value.CredentialStorageMode,
        SecretReference = value.SecretReference,
        CredentialFingerprint = value.CredentialFingerprint,
        CredentialLastFour = value.CredentialLastFour,
        HasStoredCredential = value.HasStoredCredential,
        CreatedAtUtc = value.CreatedAtUtc,
        UpdatedAtUtc = value.UpdatedAtUtc,
    };

    private static XeniaConversation Clone(XeniaConversation value) => new()
    {
        ConversationId = value.ConversationId,
        TenantId = value.TenantId,
        CreatedByUserId = value.CreatedByUserId,
        Title = value.Title,
        ActivationSource = value.ActivationSource,
        ProductCode = value.ProductCode,
        SourceReference = value.SourceReference,
        CreatedAtUtc = value.CreatedAtUtc,
        UpdatedAtUtc = value.UpdatedAtUtc,
        Messages = value.Messages.Select(Clone).ToList(),
    };

    private static XeniaConversationMessage Clone(XeniaConversationMessage value) => new()
    {
        MessageId = value.MessageId,
        ConversationId = value.ConversationId,
        Role = value.Role,
        Content = value.Content,
        ActionLabel = value.ActionLabel,
        ProductCode = value.ProductCode,
        CreatedAtUtc = value.CreatedAtUtc,
    };

    private static XeniaUsageEvent Clone(XeniaUsageEvent value) => new()
    {
        UsageEventId = value.UsageEventId,
        TenantId = value.TenantId,
        UserId = value.UserId,
        EventKind = value.EventKind,
        Provider = value.Provider,
        Model = value.Model,
        PromptTokens = value.PromptTokens,
        CompletionTokens = value.CompletionTokens,
        EstimatedCostUsd = value.EstimatedCostUsd,
        CreatedAtUtc = value.CreatedAtUtc,
    };

    private static XeniaAuditEvent Clone(XeniaAuditEvent value) => new()
    {
        AuditEventId = value.AuditEventId,
        TenantId = value.TenantId,
        EventType = value.EventType,
        ActorUserId = value.ActorUserId,
        Description = value.Description,
        CreatedAtUtc = value.CreatedAtUtc,
    };

    private static XeniaProviderHealthEvent Clone(XeniaProviderHealthEvent value) => new()
    {
        ProviderHealthEventId = value.ProviderHealthEventId,
        ProviderConfigurationId = value.ProviderConfigurationId,
        ProviderName = value.ProviderName,
        Status = value.Status,
        Message = value.Message,
        CheckedAtUtc = value.CheckedAtUtc,
    };

    private static XeniaPromptTemplate Clone(XeniaPromptTemplate value) => new()
    {
        PromptTemplateId = value.PromptTemplateId,
        TemplateCode = value.TemplateCode,
        DisplayName = value.DisplayName,
        Description = value.Description,
        Enabled = value.Enabled,
        CreatedAtUtc = value.CreatedAtUtc,
        UpdatedAtUtc = value.UpdatedAtUtc,
    };

    private static XeniaPromptVersion Clone(XeniaPromptVersion value) => new()
    {
        PromptVersionId = value.PromptVersionId,
        PromptTemplateId = value.PromptTemplateId,
        VersionNumber = value.VersionNumber,
        Content = value.Content,
        ApprovalState = value.ApprovalState,
        IsCurrent = value.IsCurrent,
        CreatedAtUtc = value.CreatedAtUtc,
        UpdatedAtUtc = value.UpdatedAtUtc,
    };

    private static XeniaSkill Clone(XeniaSkill value) => new()
    {
        SkillId = value.SkillId,
        SkillCode = value.SkillCode,
        DisplayName = value.DisplayName,
        Description = value.Description,
        Enabled = value.Enabled,
        CreatedAtUtc = value.CreatedAtUtc,
        UpdatedAtUtc = value.UpdatedAtUtc,
    };

    private static XeniaSkillVersion Clone(XeniaSkillVersion value) => new()
    {
        SkillVersionId = value.SkillVersionId,
        SkillId = value.SkillId,
        VersionNumber = value.VersionNumber,
        DefinitionJson = value.DefinitionJson,
        ApprovalState = value.ApprovalState,
        IsCurrent = value.IsCurrent,
        CreatedAtUtc = value.CreatedAtUtc,
        UpdatedAtUtc = value.UpdatedAtUtc,
    };

    private static XeniaAgent Clone(XeniaAgent value) => new()
    {
        AgentId = value.AgentId,
        AgentCode = value.AgentCode,
        DisplayName = value.DisplayName,
        Description = value.Description,
        Enabled = value.Enabled,
        CreatedAtUtc = value.CreatedAtUtc,
        UpdatedAtUtc = value.UpdatedAtUtc,
    };

    private static XeniaAgentVersion Clone(XeniaAgentVersion value) => new()
    {
        AgentVersionId = value.AgentVersionId,
        AgentId = value.AgentId,
        VersionNumber = value.VersionNumber,
        DefinitionJson = value.DefinitionJson,
        ApprovalState = value.ApprovalState,
        IsCurrent = value.IsCurrent,
        CreatedAtUtc = value.CreatedAtUtc,
        UpdatedAtUtc = value.UpdatedAtUtc,
    };

    private static XeniaKnowledgeSource Clone(XeniaKnowledgeSource value) => new()
    {
        KnowledgeSourceId = value.KnowledgeSourceId,
        TenantId = value.TenantId,
        SourceCode = value.SourceCode,
        DisplayName = value.DisplayName,
        SourceType = value.SourceType,
        Status = value.Status,
        CreatedAtUtc = value.CreatedAtUtc,
        UpdatedAtUtc = value.UpdatedAtUtc,
    };

    private static XeniaMarketplaceAsset Clone(XeniaMarketplaceAsset value) => new()
    {
        MarketplaceAssetId = value.MarketplaceAssetId,
        AssetCode = value.AssetCode,
        AssetType = value.AssetType,
        DisplayName = value.DisplayName,
        Description = value.Description,
        Enabled = value.Enabled,
        CreatedAtUtc = value.CreatedAtUtc,
        UpdatedAtUtc = value.UpdatedAtUtc,
    };

    private static XeniaMarketplaceInstallation Clone(XeniaMarketplaceInstallation value) => new()
    {
        MarketplaceInstallationId = value.MarketplaceInstallationId,
        MarketplaceAssetId = value.MarketplaceAssetId,
        TenantId = value.TenantId,
        Status = value.Status,
        InstalledAtUtc = value.InstalledAtUtc,
        UpdatedAtUtc = value.UpdatedAtUtc,
    };
}
