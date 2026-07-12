using Microsoft.Extensions.DependencyInjection;
using Xenia.Application;
using Xenia.Domain;
using Xunit;

namespace Xenia.Tests;

public class XeniaServiceTests
{
    [Fact]
    public void SaveTenantByoAiConfiguration_SwitchesTenantToBringYourOwnAi()
    {
        var service = CreateServiceProvider().GetRequiredService<IXeniaService>();
        var tenantId = Guid.Parse("30000000-0000-0000-0000-000000000111");

        var response = service.SaveTenantByoAiConfiguration(
            tenantId,
            new XeniaProviderConfigurationRequest(
                XeniaProviderType.OpenAI,
                "Tenant OpenAI",
                "https://api.openai.com/v1",
                null,
                null,
                "gpt-4.1-mini",
                ["gpt-4.1-mini"],
                60,
                2,
                10,
                true,
                "sk-test-1234"),
            "tenant-admin");

        Assert.Equal(XeniaDeploymentModel.BringYourOwnAI, response.DeploymentModel);
        Assert.NotNull(response.DefaultProviderConfigurationId);
    }

    [Fact]
    public void AddConversationMessage_PersistsUserAndAssistantMessages()
    {
        var service = CreateServiceProvider().GetRequiredService<IXeniaService>();
        var tenantId = Guid.Parse("30000000-0000-0000-0000-000000000222");

        var conversation = service.CreateConversation(
            tenantId,
            "user-1",
            new XeniaCreateConversationRequest("Draft", "UserClick", "XENIA", null, null));

        var turn = service.AddConversationMessage(
            tenantId,
            conversation.ConversationId,
            "user-1",
            new XeniaConversationMessageRequest("Summarize this lien file", "Summarize with Xenia", "XENIA"));

        Assert.Equal(XeniaMessageRole.User, turn.UserMessage.Role);
        Assert.Equal(XeniaMessageRole.Assistant, turn.AssistantMessage.Role);
        Assert.True(turn.OutputChunks.Count > 0);
        Assert.True(turn.Conversation.Messages.Count >= 2);
    }

    private static ServiceProvider CreateServiceProvider()
    {
        var services = new ServiceCollection();
        services.AddXeniaApplication();
        services.AddSingleton<IXeniaStateStore, TestXeniaStateStore>();
        services.AddSingleton<IAiProviderGateway, FakeAiProviderGateway>();
        services.AddSingleton<IAiCredentialStore, FakeAiCredentialStore>();
        services.AddSingleton<IAiUsageNormalizer, FakeAiUsageNormalizer>();
        services.AddSingleton<IAiProviderHealthCheck, FakeProviderHealthCheck>();
        services.AddSingleton<IProviderRoutingPolicy, FakeRoutingPolicy>();
        services.AddSingleton<IProviderFailoverPolicy, FakeFailoverPolicy>();
        return services.BuildServiceProvider();
    }

    private sealed class TestXeniaStateStore : IXeniaStateStore
    {
        private readonly Dictionary<Guid, XeniaTenantConfiguration> _tenantConfigurations = [];
        private readonly Dictionary<Guid, XeniaProviderConfiguration> _providerConfigurations =
            new()
            {
                [Guid.Parse("10000000-0000-0000-0000-000000003201")] = new XeniaProviderConfiguration
                {
                    ProviderConfigurationId = Guid.Parse("10000000-0000-0000-0000-000000003201"),
                    ProviderType = XeniaProviderType.OpenAI,
                    Scope = XeniaProviderScope.Platform,
                    DisplayName = "Managed OpenAI",
                    DefaultModel = "gpt-4.1-mini",
                    Enabled = true,
                },
            };
        private readonly Dictionary<Guid, XeniaConversation> _conversations = [];
        private readonly List<XeniaUsageEvent> _usageEvents = [];
        private readonly List<XeniaAuditEvent> _auditEvents = [];
        private readonly List<XeniaProviderHealthEvent> _providerHealth = [];

        public IReadOnlyList<XeniaTenantConfiguration> ListTenantConfigurations() => _tenantConfigurations.Values.ToList();
        public XeniaTenantConfiguration? FindTenantConfiguration(Guid tenantId) =>
            _tenantConfigurations.GetValueOrDefault(tenantId);
        public XeniaTenantConfiguration GetOrCreateTenantConfiguration(Guid tenantId)
        {
            if (!_tenantConfigurations.TryGetValue(tenantId, out var configuration))
            {
                configuration = new XeniaTenantConfiguration
                {
                    TenantId = tenantId,
                    DefaultProviderConfigurationId = _providerConfigurations.Keys.First(),
                    AllowedSkills = ["summary"],
                    AllowedAgents = ["assistant"],
                    AllowedTools = ["search"],
                };
                _tenantConfigurations[tenantId] = configuration;
            }

            return configuration;
        }

        public XeniaTenantConfiguration SaveTenantConfiguration(XeniaTenantConfiguration configuration)
        {
            _tenantConfigurations[configuration.TenantId] = configuration;
            return configuration;
        }

        public IReadOnlyList<XeniaProviderConfiguration> ListProviders() => _providerConfigurations.Values.ToList();

        public XeniaProviderConfiguration? GetProvider(Guid providerConfigurationId) =>
            _providerConfigurations.GetValueOrDefault(providerConfigurationId);

        public XeniaProviderConfiguration SaveProvider(XeniaProviderConfiguration providerConfiguration)
        {
            _providerConfigurations[providerConfiguration.ProviderConfigurationId] = providerConfiguration;
            return providerConfiguration;
        }

        public IReadOnlyList<XeniaModelCatalogEntryResponse> ListModels() =>
            [new("OpenAI", "gpt-4.1-mini", "GPT-4.1 Mini", true, true, true)];
        public IReadOnlyList<XeniaPromptTemplate> ListPromptTemplates() => [];
        public IReadOnlyList<XeniaPromptVersion> ListPromptVersions(Guid? promptTemplateId = null) => [];
        public IReadOnlyList<XeniaSkill> ListSkills() => [];
        public IReadOnlyList<XeniaSkillVersion> ListSkillVersions(Guid? skillId = null) => [];
        public IReadOnlyList<XeniaAgent> ListAgents() => [];
        public IReadOnlyList<XeniaAgentVersion> ListAgentVersions(Guid? agentId = null) => [];
        public IReadOnlyList<XeniaKnowledgeSource> ListKnowledgeSources(Guid? tenantId = null) => [];
        public IReadOnlyList<XeniaMarketplaceAsset> ListMarketplaceAssets() => [];
        public IReadOnlyList<XeniaMarketplaceInstallation> ListMarketplaceInstallations(Guid? tenantId = null) => [];

        public IReadOnlyList<XeniaConversation> ListConversations(Guid tenantId) =>
            _conversations.Values.Where(item => item.TenantId == tenantId).ToList();

        public XeniaConversation? GetConversation(Guid tenantId, Guid conversationId) =>
            _conversations.GetValueOrDefault(conversationId);

        public XeniaConversation SaveConversation(XeniaConversation conversation)
        {
            _conversations[conversation.ConversationId] = conversation;
            return conversation;
        }

        public IReadOnlyList<XeniaUsageEvent> ListUsage() => _usageEvents;
        public void AppendUsage(XeniaUsageEvent usageEvent) => _usageEvents.Add(usageEvent);
        public IReadOnlyList<XeniaAuditEvent> ListAudit() => _auditEvents;
        public void AppendAudit(XeniaAuditEvent auditEvent) => _auditEvents.Add(auditEvent);
        public IReadOnlyList<XeniaProviderHealthEvent> ListProviderHealth() => _providerHealth;
        public void AppendProviderHealth(XeniaProviderHealthEvent providerHealthEvent) => _providerHealth.Add(providerHealthEvent);
    }

    private sealed class FakeAiProviderGateway : IAiProviderGateway
    {
        public XeniaAiResponse Execute(XeniaProviderConfiguration provider, XeniaAiExecutionContext context) =>
            new(
                provider.DisplayName,
                provider.DefaultModel,
                $"Synthetic provider output for {context.RequestKind}: {context.Prompt}",
                ["Synthetic provider output"],
                10,
                15,
                0.00025m,
                12,
                true);

        public XeniaProviderValidationResult Validate(XeniaProviderConfiguration provider, XeniaResolvedCredential credential) =>
            new(true, "Connected", "Validated", DateTime.UtcNow, credential.Fingerprint);
    }

    private sealed class FakeAiCredentialStore : IAiCredentialStore
    {
        public XeniaProviderCredentialRecord Save(Guid providerConfigurationId, XeniaCredentialStorageMode storageMode, string? apiKey, string? externalSecretReference) =>
            new(Guid.CreateVersion7(), providerConfigurationId, storageMode, "fp-1234", "1234", DateTime.UtcNow, XeniaVerificationStatus.Verified, true, DateTime.UtcNow, DateTime.UtcNow);

        public XeniaProviderCredentialRecord? GetActiveMetadata(Guid providerConfigurationId) => null;

        public XeniaResolvedCredential? Resolve(Guid providerConfigurationId, XeniaProviderConfiguration provider) =>
            new(XeniaCredentialStorageMode.EncryptedDatabase, "sk-test-1234", "fp-1234", "1234");
    }

    private sealed class FakeAiUsageNormalizer : IAiUsageNormalizer
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

    private sealed class FakeProviderHealthCheck : IAiProviderHealthCheck
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

    private sealed class FakeRoutingPolicy : IProviderRoutingPolicy
    {
        public XeniaProviderConfiguration Resolve(XeniaTenantConfiguration configuration, Guid tenantId, IReadOnlyList<XeniaProviderConfiguration> providers) =>
            providers.First();
    }

    private sealed class FakeFailoverPolicy : IProviderFailoverPolicy
    {
        public XeniaProviderConfiguration SelectFallback(XeniaTenantConfiguration configuration, Guid tenantId, IReadOnlyList<XeniaProviderConfiguration> providers, Guid failedProviderConfigurationId) =>
            providers.First();
    }
}
