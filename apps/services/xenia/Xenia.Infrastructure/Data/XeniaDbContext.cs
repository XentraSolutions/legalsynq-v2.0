using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Xenia.Domain;

namespace Xenia.Infrastructure.Data;

public sealed class XeniaDbContext(DbContextOptions<XeniaDbContext> options) : DbContext(options)
{
    public DbSet<XeniaTenantConfiguration> TenantConfigurations => Set<XeniaTenantConfiguration>();
    public DbSet<XeniaProviderConfiguration> ProviderConfigurations => Set<XeniaProviderConfiguration>();
    public DbSet<XeniaProviderCredential> ProviderCredentials => Set<XeniaProviderCredential>();
    public DbSet<XeniaModelCatalogEntry> ModelCatalogEntries => Set<XeniaModelCatalogEntry>();
    public DbSet<XeniaTenantModelPolicy> TenantModelPolicies => Set<XeniaTenantModelPolicy>();
    public DbSet<XeniaBudgetPolicy> BudgetPolicies => Set<XeniaBudgetPolicy>();
    public DbSet<XeniaQuotaPolicy> QuotaPolicies => Set<XeniaQuotaPolicy>();
    public DbSet<XeniaConversation> Conversations => Set<XeniaConversation>();
    public DbSet<XeniaConversationMessage> ConversationMessages => Set<XeniaConversationMessage>();
    public DbSet<XeniaPromptTemplate> PromptTemplates => Set<XeniaPromptTemplate>();
    public DbSet<XeniaPromptVersion> PromptVersions => Set<XeniaPromptVersion>();
    public DbSet<XeniaSkill> Skills => Set<XeniaSkill>();
    public DbSet<XeniaSkillVersion> SkillVersions => Set<XeniaSkillVersion>();
    public DbSet<XeniaAgent> Agents => Set<XeniaAgent>();
    public DbSet<XeniaAgentVersion> AgentVersions => Set<XeniaAgentVersion>();
    public DbSet<XeniaAgentSkillLink> AgentSkillLinks => Set<XeniaAgentSkillLink>();
    public DbSet<XeniaAgentToolLink> AgentToolLinks => Set<XeniaAgentToolLink>();
    public DbSet<XeniaAgentKnowledgeLink> AgentKnowledgeLinks => Set<XeniaAgentKnowledgeLink>();
    public DbSet<XeniaKnowledgeSource> KnowledgeSources => Set<XeniaKnowledgeSource>();
    public DbSet<XeniaKnowledgeDocument> KnowledgeDocuments => Set<XeniaKnowledgeDocument>();
    public DbSet<XeniaKnowledgeChunk> KnowledgeChunks => Set<XeniaKnowledgeChunk>();
    public DbSet<XeniaEmbeddingIndex> EmbeddingIndexes => Set<XeniaEmbeddingIndex>();
    public DbSet<XeniaEmbeddingRecord> EmbeddingRecords => Set<XeniaEmbeddingRecord>();
    public DbSet<XeniaCitation> Citations => Set<XeniaCitation>();
    public DbSet<XeniaToolDefinition> ToolDefinitions => Set<XeniaToolDefinition>();
    public DbSet<XeniaToolExecutionLog> ToolExecutionLogs => Set<XeniaToolExecutionLog>();
    public DbSet<XeniaAiRequestLog> AiRequestLogs => Set<XeniaAiRequestLog>();
    public DbSet<XeniaUsageEvent> UsageLedger => Set<XeniaUsageEvent>();
    public DbSet<XeniaCostLedgerEntry> CostLedger => Set<XeniaCostLedgerEntry>();
    public DbSet<XeniaProviderHealthEvent> ProviderHealthEvents => Set<XeniaProviderHealthEvent>();
    public DbSet<XeniaGovernanceEvent> GovernanceEvents => Set<XeniaGovernanceEvent>();
    public DbSet<XeniaAuditEvent> AuditEvents => Set<XeniaAuditEvent>();
    public DbSet<XeniaMarketplaceAsset> MarketplaceAssets => Set<XeniaMarketplaceAsset>();
    public DbSet<XeniaMarketplaceInstallation> MarketplaceInstallations => Set<XeniaMarketplaceInstallation>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var stringListConverter = new ValueConverter<List<string>, string>(
            value => JsonSerializer.Serialize(value ?? new List<string>(), (JsonSerializerOptions?)null),
            value => string.IsNullOrWhiteSpace(value)
                ? new List<string>()
                : JsonSerializer.Deserialize<List<string>>(value) ?? new List<string>());

        var stringListComparer = new ValueComparer<List<string>>(
            (left, right) => JsonSerializer.Serialize(left ?? new List<string>(), (JsonSerializerOptions?)null) == JsonSerializer.Serialize(right ?? new List<string>(), (JsonSerializerOptions?)null),
            value => JsonSerializer.Serialize(value ?? new List<string>(), (JsonSerializerOptions?)null).GetHashCode(),
            value => value == null ? new List<string>() : value.ToList());

        ConfigureCommon(modelBuilder, stringListConverter, stringListComparer);
    }

    public override int SaveChanges()
    {
        UpdateAuditTimestamps();
        return base.SaveChanges();
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        UpdateAuditTimestamps();
        return base.SaveChangesAsync(cancellationToken);
    }

    private void UpdateAuditTimestamps()
    {
        var now = DateTime.UtcNow;

        foreach (var entry in ChangeTracker.Entries())
        {
            if (entry.State != EntityState.Modified) continue;

            var property = entry.Properties.FirstOrDefault(candidate => candidate.Metadata.Name == "UpdatedAtUtc");
            if (property is not null)
                property.CurrentValue = now;
        }
    }

    private static void ConfigureCommon(
        ModelBuilder modelBuilder,
        ValueConverter<List<string>, string> stringListConverter,
        ValueComparer<List<string>> stringListComparer)
    {
        modelBuilder.Entity<XeniaTenantConfiguration>(builder =>
        {
            builder.ToTable("xen_tenant_ai_configurations");
            builder.HasKey(x => x.TenantId);
            ConfigureStringList(builder.Property(x => x.AllowedSkills), stringListConverter, stringListComparer);
            ConfigureStringList(builder.Property(x => x.AllowedAgents), stringListConverter, stringListComparer);
            ConfigureStringList(builder.Property(x => x.AllowedTools), stringListConverter, stringListComparer);
        });

        modelBuilder.Entity<XeniaProviderConfiguration>(builder =>
        {
            builder.ToTable("xen_provider_configurations");
            builder.HasKey(x => x.ProviderConfigurationId);
            builder.Property(x => x.DisplayName).HasMaxLength(256);
            builder.HasIndex(x => new { x.Scope, x.TenantId, x.DisplayName }).IsUnique();
            ConfigureStringList(builder.Property(x => x.AllowedModels), stringListConverter, stringListComparer);
        });

        modelBuilder.Entity<XeniaProviderCredential>(builder =>
        {
            builder.ToTable("xen_provider_credentials");
            builder.HasKey(x => x.ProviderCredentialId);
            builder.HasIndex(x => new { x.ProviderConfigurationId, x.IsActive });
        });

        modelBuilder.Entity<XeniaModelCatalogEntry>(builder =>
        {
            builder.ToTable("xen_model_catalog");
            builder.HasKey(x => x.ModelCatalogEntryId);
            builder.Property(x => x.Provider).HasMaxLength(64);
            builder.Property(x => x.ModelCode).HasMaxLength(128);
            builder.HasIndex(x => new { x.Provider, x.ModelCode }).IsUnique();
        });

        modelBuilder.Entity<XeniaTenantModelPolicy>(builder =>
        {
            builder.ToTable("xen_tenant_model_policies");
            builder.HasKey(x => x.TenantModelPolicyId);
            ConfigureStringList(builder.Property(x => x.AllowedProviders), stringListConverter, stringListComparer);
            ConfigureStringList(builder.Property(x => x.AllowedModels), stringListConverter, stringListComparer);
        });

        modelBuilder.Entity<XeniaBudgetPolicy>().ToTable("xen_budget_policies").HasKey(x => x.BudgetPolicyId);
        modelBuilder.Entity<XeniaQuotaPolicy>().ToTable("xen_quota_policies").HasKey(x => x.QuotaPolicyId);

        modelBuilder.Entity<XeniaConversation>(builder =>
        {
            builder.ToTable("xen_conversations");
            builder.HasKey(x => x.ConversationId);
            builder.Ignore(x => x.Messages);
            builder.HasIndex(x => new { x.TenantId, x.UpdatedAtUtc });
        });

        modelBuilder.Entity<XeniaConversationMessage>(builder =>
        {
            builder.ToTable("xen_conversation_messages");
            builder.HasKey(x => x.MessageId);
            builder.HasIndex(x => new { x.ConversationId, x.CreatedAtUtc });
        });

        modelBuilder.Entity<XeniaPromptTemplate>().ToTable("xen_prompt_templates").HasKey(x => x.PromptTemplateId);
        modelBuilder.Entity<XeniaPromptVersion>().ToTable("xen_prompt_versions").HasKey(x => x.PromptVersionId);
        modelBuilder.Entity<XeniaSkill>().ToTable("xen_skills").HasKey(x => x.SkillId);
        modelBuilder.Entity<XeniaSkillVersion>().ToTable("xen_skill_versions").HasKey(x => x.SkillVersionId);
        modelBuilder.Entity<XeniaAgent>().ToTable("xen_agents").HasKey(x => x.AgentId);
        modelBuilder.Entity<XeniaAgentVersion>().ToTable("xen_agent_versions").HasKey(x => x.AgentVersionId);
        modelBuilder.Entity<XeniaAgentSkillLink>().ToTable("xen_agent_skill_links").HasKey(x => x.AgentSkillLinkId);
        modelBuilder.Entity<XeniaAgentToolLink>().ToTable("xen_agent_tool_links").HasKey(x => x.AgentToolLinkId);
        modelBuilder.Entity<XeniaAgentKnowledgeLink>().ToTable("xen_agent_knowledge_links").HasKey(x => x.AgentKnowledgeLinkId);
        modelBuilder.Entity<XeniaKnowledgeSource>().ToTable("xen_knowledge_sources").HasKey(x => x.KnowledgeSourceId);
        modelBuilder.Entity<XeniaKnowledgeDocument>().ToTable("xen_knowledge_documents").HasKey(x => x.KnowledgeDocumentId);
        modelBuilder.Entity<XeniaKnowledgeChunk>().ToTable("xen_knowledge_chunks").HasKey(x => x.KnowledgeChunkId);
        modelBuilder.Entity<XeniaEmbeddingIndex>().ToTable("xen_embedding_indexes").HasKey(x => x.EmbeddingIndexId);
        modelBuilder.Entity<XeniaEmbeddingRecord>().ToTable("xen_embedding_records").HasKey(x => x.EmbeddingRecordId);
        modelBuilder.Entity<XeniaCitation>().ToTable("xen_citations").HasKey(x => x.CitationId);
        modelBuilder.Entity<XeniaToolDefinition>().ToTable("xen_tool_definitions").HasKey(x => x.ToolDefinitionId);
        modelBuilder.Entity<XeniaToolExecutionLog>().ToTable("xen_tool_execution_logs").HasKey(x => x.ToolExecutionLogId);
        modelBuilder.Entity<XeniaAiRequestLog>().ToTable("xen_ai_request_logs").HasKey(x => x.AiRequestLogId);
        modelBuilder.Entity<XeniaUsageEvent>().ToTable("xen_usage_ledger").HasKey(x => x.UsageEventId);
        modelBuilder.Entity<XeniaCostLedgerEntry>().ToTable("xen_cost_ledger").HasKey(x => x.CostLedgerEntryId);
        modelBuilder.Entity<XeniaProviderHealthEvent>().ToTable("xen_provider_health_events").HasKey(x => x.ProviderHealthEventId);
        modelBuilder.Entity<XeniaGovernanceEvent>().ToTable("xen_governance_events").HasKey(x => x.GovernanceEventId);
        modelBuilder.Entity<XeniaAuditEvent>().ToTable("xen_audit_events").HasKey(x => x.AuditEventId);
        modelBuilder.Entity<XeniaMarketplaceAsset>().ToTable("xen_marketplace_assets").HasKey(x => x.MarketplaceAssetId);
        modelBuilder.Entity<XeniaMarketplaceInstallation>().ToTable("xen_marketplace_installations").HasKey(x => x.MarketplaceInstallationId);
    }

    private static void ConfigureStringList(
        PropertyBuilder<List<string>> builder,
        ValueConverter<List<string>, string> converter,
        ValueComparer<List<string>> comparer)
    {
        builder.HasConversion(converter);
        builder.Metadata.SetValueComparer(comparer);
        builder.HasColumnType("longtext");
    }

}
