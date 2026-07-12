using Microsoft.EntityFrameworkCore;
using Xenia.Domain;

namespace Xenia.Infrastructure.Data;

public static class XeniaSeedData
{
    public static async Task InitializeAsync(XeniaDbContext dbContext, CancellationToken cancellationToken = default)
    {
        var seededAt = new DateTime(2026, 7, 9, 0, 0, 0, DateTimeKind.Utc);

        if (!await dbContext.ProviderConfigurations.AnyAsync(cancellationToken))
        {
            dbContext.ProviderConfigurations.AddRange(
                new XeniaProviderConfiguration
                {
                    ProviderConfigurationId = Guid.Parse("10000000-0000-0000-0000-000000003201"),
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
                },
                new XeniaProviderConfiguration
                {
                    ProviderConfigurationId = Guid.Parse("10000000-0000-0000-0000-000000003202"),
                    ProviderType = XeniaProviderType.Anthropic,
                    Scope = XeniaProviderScope.Platform,
                    DisplayName = "Managed Anthropic",
                    DefaultModel = "claude-3-7-sonnet",
                    AllowedModels = ["claude-3-7-sonnet"],
                    FailoverPriority = 20,
                    Enabled = true,
                    CredentialStorageMode = XeniaCredentialStorageMode.ExternalSecretReference,
                    SecretReference = "platform://xenia/anthropic",
                    CredentialFingerprint = "fp-pic",
                    CredentialLastFour = "pic",
                    HasStoredCredential = true,
                    VerificationStatus = XeniaVerificationStatus.Unverified,
                    CreatedAtUtc = seededAt,
                    UpdatedAtUtc = seededAt,
                },
                new XeniaProviderConfiguration
                {
                    ProviderConfigurationId = Guid.Parse("10000000-0000-0000-0000-000000003203"),
                    ProviderType = XeniaProviderType.Gemini,
                    Scope = XeniaProviderScope.Platform,
                    DisplayName = "Managed Gemini",
                    DefaultModel = "gemini-2.5-pro",
                    AllowedModels = ["gemini-2.5-pro"],
                    FailoverPriority = 30,
                    Enabled = true,
                    CredentialStorageMode = XeniaCredentialStorageMode.ExternalSecretReference,
                    SecretReference = "platform://xenia/gemini",
                    CredentialFingerprint = "fp-ini",
                    CredentialLastFour = "ini",
                    HasStoredCredential = true,
                    VerificationStatus = XeniaVerificationStatus.Unverified,
                    CreatedAtUtc = seededAt,
                    UpdatedAtUtc = seededAt,
                });
        }

        if (!await dbContext.ModelCatalogEntries.AnyAsync(cancellationToken))
        {
            dbContext.ModelCatalogEntries.AddRange(
                new XeniaModelCatalogEntry
                {
                    ModelCatalogEntryId = Guid.Parse("10000000-0000-0000-0000-000000003301"),
                    Provider = "OpenAI",
                    ModelCode = "gpt-4.1-mini",
                    DisplayName = "GPT-4.1 Mini",
                    SupportsStreaming = true,
                    SupportsEmbeddings = true,
                    Enabled = true,
                    ContextSize = 128000,
                    CreatedAtUtc = seededAt,
                    UpdatedAtUtc = seededAt,
                },
                new XeniaModelCatalogEntry
                {
                    ModelCatalogEntryId = Guid.Parse("10000000-0000-0000-0000-000000003302"),
                    Provider = "Anthropic",
                    ModelCode = "claude-3-7-sonnet",
                    DisplayName = "Claude 3.7 Sonnet",
                    SupportsStreaming = true,
                    SupportsEmbeddings = false,
                    Enabled = true,
                    ContextSize = 200000,
                    CreatedAtUtc = seededAt,
                    UpdatedAtUtc = seededAt,
                },
                new XeniaModelCatalogEntry
                {
                    ModelCatalogEntryId = Guid.Parse("10000000-0000-0000-0000-000000003303"),
                    Provider = "Gemini",
                    ModelCode = "gemini-2.5-pro",
                    DisplayName = "Gemini 2.5 Pro",
                    SupportsStreaming = true,
                    SupportsEmbeddings = true,
                    Enabled = true,
                    ContextSize = 1000000,
                    CreatedAtUtc = seededAt,
                    UpdatedAtUtc = seededAt,
                },
                new XeniaModelCatalogEntry
                {
                    ModelCatalogEntryId = Guid.Parse("10000000-0000-0000-0000-000000003304"),
                    Provider = "AzureOpenAI",
                    ModelCode = "gpt-4.1-mini",
                    DisplayName = "GPT-4.1 Mini (Azure)",
                    SupportsStreaming = true,
                    SupportsEmbeddings = true,
                    Enabled = true,
                    ContextSize = 128000,
                    CreatedAtUtc = seededAt,
                    UpdatedAtUtc = seededAt,
                },
                new XeniaModelCatalogEntry
                {
                    ModelCatalogEntryId = Guid.Parse("10000000-0000-0000-0000-000000003305"),
                    Provider = "AwsBedrock",
                    ModelCode = "anthropic.claude-3-5-sonnet",
                    DisplayName = "Claude 3.5 Sonnet via Bedrock",
                    SupportsStreaming = true,
                    SupportsEmbeddings = false,
                    Enabled = true,
                    ContextSize = 200000,
                    CreatedAtUtc = seededAt,
                    UpdatedAtUtc = seededAt,
                });
        }

        if (!await dbContext.PromptTemplates.AnyAsync(cancellationToken))
        {
            var promptTemplateId = Guid.Parse("10000000-0000-0000-0000-000000003401");
            dbContext.PromptTemplates.Add(new XeniaPromptTemplate
            {
                PromptTemplateId = promptTemplateId,
                TemplateCode = "general-summary",
                DisplayName = "General Summary",
                Description = "Default summary prompt template for cross-product Xenia work.",
                Enabled = true,
                CreatedAtUtc = seededAt,
                UpdatedAtUtc = seededAt,
            });
            dbContext.PromptVersions.Add(new XeniaPromptVersion
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
        }

        if (!await dbContext.Skills.AnyAsync(cancellationToken))
        {
            var skillId = Guid.Parse("10000000-0000-0000-0000-000000003411");
            dbContext.Skills.Add(new XeniaSkill
            {
                SkillId = skillId,
                SkillCode = "summary",
                DisplayName = "Summary",
                Description = "Summarize documents, cases, or workflow state into a concise operational brief.",
                Enabled = true,
                CreatedAtUtc = seededAt,
                UpdatedAtUtc = seededAt,
            });
            dbContext.SkillVersions.Add(new XeniaSkillVersion
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
        }

        if (!await dbContext.Agents.AnyAsync(cancellationToken))
        {
            var agentId = Guid.Parse("10000000-0000-0000-0000-000000003421");
            var agentVersionId = Guid.Parse("10000000-0000-0000-0000-000000003422");
            dbContext.Agents.Add(new XeniaAgent
            {
                AgentId = agentId,
                AgentCode = "workspace-assistant",
                DisplayName = "Workspace Assistant",
                Description = "Default tenant-facing Xenia assistant for summaries, drafts, and operational reasoning.",
                Enabled = true,
                CreatedAtUtc = seededAt,
                UpdatedAtUtc = seededAt,
            });
            dbContext.AgentVersions.Add(new XeniaAgentVersion
            {
                AgentVersionId = agentVersionId,
                AgentId = agentId,
                VersionNumber = 1,
                DefinitionJson = "{\"style\":\"operational\",\"approvalRequired\":true}",
                ApprovalState = "Approved",
                IsCurrent = true,
                CreatedAtUtc = seededAt,
                UpdatedAtUtc = seededAt,
            });
            dbContext.AgentSkillLinks.Add(new XeniaAgentSkillLink
            {
                AgentSkillLinkId = Guid.Parse("10000000-0000-0000-0000-000000003423"),
                AgentVersionId = agentVersionId,
                SkillVersionId = Guid.Parse("10000000-0000-0000-0000-000000003412"),
                CreatedAtUtc = seededAt,
            });
        }

        if (!await dbContext.KnowledgeSources.AnyAsync(cancellationToken))
        {
            dbContext.KnowledgeSources.Add(new XeniaKnowledgeSource
            {
                KnowledgeSourceId = Guid.Parse("10000000-0000-0000-0000-000000003431"),
                SourceCode = "platform-docs",
                DisplayName = "Platform Documentation",
                SourceType = "DocumentSet",
                Status = "Ready",
                CreatedAtUtc = seededAt,
                UpdatedAtUtc = seededAt,
            });
        }

        if (!await dbContext.ToolDefinitions.AnyAsync(cancellationToken))
        {
            dbContext.ToolDefinitions.Add(new XeniaToolDefinition
            {
                ToolDefinitionId = Guid.Parse("10000000-0000-0000-0000-000000003441"),
                ToolCode = "document-search",
                DisplayName = "Document Search",
                Description = "Search platform documents with tenant isolation preserved.",
                RequiredPermission = "XENIA.tool:document-search",
                Enabled = true,
                CreatedAtUtc = seededAt,
                UpdatedAtUtc = seededAt,
            });
        }

        if (!await dbContext.MarketplaceAssets.AnyAsync(cancellationToken))
        {
            dbContext.MarketplaceAssets.Add(new XeniaMarketplaceAsset
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

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
