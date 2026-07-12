namespace Xenia.Domain;

public sealed class XeniaModelCatalogEntry
{
    public Guid ModelCatalogEntryId { get; set; }
    public string Provider { get; set; } = string.Empty;
    public string ModelCode { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public bool SupportsStreaming { get; set; }
    public bool SupportsEmbeddings { get; set; }
    public bool Enabled { get; set; } = true;
    public int? ContextSize { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class XeniaTenantModelPolicy
{
    public Guid TenantModelPolicyId { get; set; }
    public Guid TenantId { get; set; }
    public string PolicyName { get; set; } = string.Empty;
    public List<string> AllowedProviders { get; set; } = [];
    public List<string> AllowedModels { get; set; } = [];
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class XeniaBudgetPolicy
{
    public Guid BudgetPolicyId { get; set; }
    public Guid? TenantId { get; set; }
    public string PolicyName { get; set; } = string.Empty;
    public decimal SoftLimitUsd { get; set; }
    public decimal HardLimitUsd { get; set; }
    public bool BlockOnHardLimit { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class XeniaQuotaPolicy
{
    public Guid QuotaPolicyId { get; set; }
    public Guid? TenantId { get; set; }
    public string PolicyName { get; set; } = string.Empty;
    public int MonthlyRequestLimit { get; set; }
    public int MonthlyTokenLimit { get; set; }
    public bool BlockOnLimit { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class XeniaPromptTemplate
{
    public Guid PromptTemplateId { get; set; }
    public string TemplateCode { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class XeniaPromptVersion
{
    public Guid PromptVersionId { get; set; }
    public Guid PromptTemplateId { get; set; }
    public int VersionNumber { get; set; }
    public string Content { get; set; } = string.Empty;
    public string ApprovalState { get; set; } = "Draft";
    public bool IsCurrent { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class XeniaSkill
{
    public Guid SkillId { get; set; }
    public string SkillCode { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class XeniaSkillVersion
{
    public Guid SkillVersionId { get; set; }
    public Guid SkillId { get; set; }
    public int VersionNumber { get; set; }
    public string DefinitionJson { get; set; } = "{}";
    public string ApprovalState { get; set; } = "Draft";
    public bool IsCurrent { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class XeniaAgent
{
    public Guid AgentId { get; set; }
    public string AgentCode { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class XeniaAgentVersion
{
    public Guid AgentVersionId { get; set; }
    public Guid AgentId { get; set; }
    public int VersionNumber { get; set; }
    public string DefinitionJson { get; set; } = "{}";
    public string ApprovalState { get; set; } = "Draft";
    public bool IsCurrent { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class XeniaAgentSkillLink
{
    public Guid AgentSkillLinkId { get; set; }
    public Guid AgentVersionId { get; set; }
    public Guid SkillVersionId { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class XeniaAgentToolLink
{
    public Guid AgentToolLinkId { get; set; }
    public Guid AgentVersionId { get; set; }
    public Guid ToolDefinitionId { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class XeniaAgentKnowledgeLink
{
    public Guid AgentKnowledgeLinkId { get; set; }
    public Guid AgentVersionId { get; set; }
    public Guid KnowledgeSourceId { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class XeniaKnowledgeSource
{
    public Guid KnowledgeSourceId { get; set; }
    public Guid? TenantId { get; set; }
    public string SourceCode { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string SourceType { get; set; } = string.Empty;
    public string Status { get; set; } = "Pending";
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class XeniaKnowledgeDocument
{
    public Guid KnowledgeDocumentId { get; set; }
    public Guid KnowledgeSourceId { get; set; }
    public string ExternalDocumentId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Status { get; set; } = "Pending";
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class XeniaKnowledgeChunk
{
    public Guid KnowledgeChunkId { get; set; }
    public Guid KnowledgeDocumentId { get; set; }
    public int ChunkIndex { get; set; }
    public string Content { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class XeniaEmbeddingIndex
{
    public Guid EmbeddingIndexId { get; set; }
    public string IndexCode { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    public string ModelCode { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class XeniaEmbeddingRecord
{
    public Guid EmbeddingRecordId { get; set; }
    public Guid EmbeddingIndexId { get; set; }
    public Guid KnowledgeChunkId { get; set; }
    public string VectorJson { get; set; } = "[]";
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class XeniaCitation
{
    public Guid CitationId { get; set; }
    public Guid KnowledgeDocumentId { get; set; }
    public Guid? ConversationId { get; set; }
    public Guid? MessageId { get; set; }
    public string ReferenceText { get; set; } = string.Empty;
    public string LocationHint { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class XeniaToolDefinition
{
    public Guid ToolDefinitionId { get; set; }
    public string ToolCode { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string RequiredPermission { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class XeniaToolExecutionLog
{
    public Guid ToolExecutionLogId { get; set; }
    public Guid ToolDefinitionId { get; set; }
    public Guid TenantId { get; set; }
    public string ProductCode { get; set; } = string.Empty;
    public string ActorUserId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string RequestJson { get; set; } = "{}";
    public string ResponseJson { get; set; } = "{}";
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class XeniaAiRequestLog
{
    public Guid AiRequestLogId { get; set; }
    public Guid TenantId { get; set; }
    public string ProductCode { get; set; } = string.Empty;
    public string ActorUserId { get; set; } = string.Empty;
    public Guid? ConversationId { get; set; }
    public Guid? ProviderConfigurationId { get; set; }
    public string RequestKind { get; set; } = string.Empty;
    public string ActivationSource { get; set; } = string.Empty;
    public string AuditClassification { get; set; } = string.Empty;
    public string? SourceObjectReferencesJson { get; set; }
    public string? ApplyOutcome { get; set; }
    public string RequestJson { get; set; } = "{}";
    public string ResponseJson { get; set; } = "{}";
    public long DurationMs { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class XeniaCostLedgerEntry
{
    public Guid CostLedgerEntryId { get; set; }
    public Guid TenantId { get; set; }
    public Guid UsageEventId { get; set; }
    public string ProductCode { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public decimal EstimatedCostUsd { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class XeniaGovernanceEvent
{
    public Guid GovernanceEventId { get; set; }
    public Guid TenantId { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string Severity { get; set; } = "Info";
    public string Description { get; set; } = string.Empty;
    public string MetadataJson { get; set; } = "{}";
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class XeniaMarketplaceAsset
{
    public Guid MarketplaceAssetId { get; set; }
    public string AssetCode { get; set; } = string.Empty;
    public string AssetType { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class XeniaMarketplaceInstallation
{
    public Guid MarketplaceInstallationId { get; set; }
    public Guid MarketplaceAssetId { get; set; }
    public Guid TenantId { get; set; }
    public string Status { get; set; } = "Installed";
    public DateTime InstalledAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
