using Xenia.Domain;

namespace Xenia.Application;

public sealed record XeniaTenantConfigurationRequest(
    bool Enabled,
    XeniaDeploymentModel DeploymentModel,
    Guid? DefaultProviderConfigurationId,
    string DefaultModel,
    double Temperature,
    int MaxTokens,
    string ReasoningLevel,
    string RetentionPolicy,
    string ModerationPolicy,
    bool FailoverEnabled,
    IReadOnlyList<string>? AllowedSkills,
    IReadOnlyList<string>? AllowedAgents,
    IReadOnlyList<string>? AllowedTools);

public sealed record XeniaTenantConfigurationResponse(
    Guid TenantId,
    bool Enabled,
    XeniaDeploymentModel DeploymentModel,
    Guid? DefaultProviderConfigurationId,
    string DefaultModel,
    double Temperature,
    int MaxTokens,
    string ReasoningLevel,
    string RetentionPolicy,
    string ModerationPolicy,
    bool FailoverEnabled,
    IReadOnlyList<string> AllowedSkills,
    IReadOnlyList<string> AllowedAgents,
    IReadOnlyList<string> AllowedTools,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);

public sealed record XeniaProviderConfigurationRequest(
    XeniaProviderType ProviderType,
    string DisplayName,
    string? Endpoint,
    string? Region,
    string? AzureDeploymentName,
    string DefaultModel,
    IReadOnlyList<string>? AllowedModels,
    int TimeoutSeconds,
    int RetryCount,
    int FailoverPriority,
    bool Enabled,
    string? ApiKey,
    XeniaCredentialStorageMode CredentialStorageMode = XeniaCredentialStorageMode.EncryptedDatabase,
    string? ExternalSecretReference = null);

public sealed record XeniaProviderConfigurationResponse(
    Guid ProviderConfigurationId,
    XeniaProviderType ProviderType,
    XeniaProviderScope Scope,
    Guid? TenantId,
    string DisplayName,
    string? Endpoint,
    string? Region,
    string? AzureDeploymentName,
    string DefaultModel,
    IReadOnlyList<string> AllowedModels,
    int TimeoutSeconds,
    int RetryCount,
    int FailoverPriority,
    bool Enabled,
    XeniaVerificationStatus VerificationStatus,
    DateTime? LastVerifiedAtUtc,
    XeniaCredentialStorageMode CredentialStorageMode,
    string? SecretReference,
    string? CredentialFingerprint,
    string? CredentialLastFour,
    bool HasStoredCredential,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);

public sealed record XeniaProviderTestResponse(
    bool Success,
    string Status,
    string Message,
    DateTime VerifiedAtUtc,
    string? Fingerprint);

public sealed record XeniaModelCatalogEntryResponse(
    string Provider,
    string ModelCode,
    string DisplayName,
    bool SupportsStreaming,
    bool SupportsEmbeddings,
    bool Enabled);

public sealed record XeniaUsageSummaryResponse(
    int RequestCount,
    int PromptTokens,
    int CompletionTokens,
    decimal EstimatedCostUsd);

public sealed record XeniaUsageEventResponse(
    Guid UsageEventId,
    Guid TenantId,
    string UserId,
    string EventKind,
    string Provider,
    string Model,
    int PromptTokens,
    int CompletionTokens,
    decimal EstimatedCostUsd,
    DateTime CreatedAtUtc);

public sealed record XeniaAuditEventResponse(
    Guid AuditEventId,
    Guid TenantId,
    string EventType,
    string ActorUserId,
    string Description,
    DateTime CreatedAtUtc);

public sealed record XeniaProviderHealthResponse(
    Guid ProviderConfigurationId,
    string ProviderName,
    string Status,
    string Message,
    DateTime CheckedAtUtc);

public sealed record XeniaAdminOverviewResponse(
    int EnabledTenantCount,
    IReadOnlyDictionary<string, int> DeploymentModelDistribution,
    int ProviderCount,
    int ConversationCount,
    XeniaUsageSummaryResponse Usage,
    IReadOnlyList<XeniaProviderHealthResponse> ProviderHealth);

public sealed record XeniaCreateConversationRequest(
    string Title,
    string ActivationSource,
    string? ProductCode,
    string? SourceReference,
    string? InitialMessage);

public sealed record XeniaConversationMessageRequest(
    string Content,
    string? ActionLabel,
    string? ProductCode);

public sealed record XeniaConversationMessageResponse(
    Guid MessageId,
    XeniaMessageRole Role,
    string Content,
    string? ActionLabel,
    string? ProductCode,
    DateTime CreatedAtUtc);

public sealed record XeniaConversationResponse(
    Guid ConversationId,
    Guid TenantId,
    string CreatedByUserId,
    string Title,
    string ActivationSource,
    string? ProductCode,
    string? SourceReference,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    IReadOnlyList<XeniaConversationMessageResponse> Messages);

public sealed record XeniaConversationTurnResponse(
    XeniaConversationResponse Conversation,
    XeniaConversationMessageResponse UserMessage,
    XeniaConversationMessageResponse AssistantMessage,
    IReadOnlyList<string> OutputChunks,
    XeniaUsageSummaryResponse Usage);

public sealed record XeniaExecutionRequest(
    Guid? TenantId,
    string? ProductCode,
    string Prompt,
    string? ActivationSource,
    IReadOnlyDictionary<string, string>? Context,
    string? AuditClassification = null,
    string? ActionLabel = null,
    string? ApplyOutcome = null,
    IReadOnlyList<string>? SourceObjectReferences = null);

public sealed record XeniaExecutionResponse(
    string Mode,
    Guid TenantId,
    string ProductCode,
    string Provider,
    string Model,
    string Output,
    IReadOnlyList<string> OutputChunks,
    XeniaUsageSummaryResponse Usage,
    string ActivationSource,
    string? SkillCode,
    string? AgentCode,
    string? ToolCode);

public sealed record XeniaUsageReportResponse(
    XeniaUsageSummaryResponse Summary,
    IReadOnlyList<XeniaUsageEventResponse> Items);

public sealed record XeniaPromptTemplateResponse(
    Guid PromptTemplateId,
    string TemplateCode,
    string DisplayName,
    string Description,
    bool Enabled,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);

public sealed record XeniaPromptVersionResponse(
    Guid PromptVersionId,
    Guid PromptTemplateId,
    int VersionNumber,
    string Content,
    string ApprovalState,
    bool IsCurrent,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);

public sealed record XeniaSkillResponse(
    Guid SkillId,
    string SkillCode,
    string DisplayName,
    string Description,
    bool Enabled,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);

public sealed record XeniaSkillVersionResponse(
    Guid SkillVersionId,
    Guid SkillId,
    int VersionNumber,
    string DefinitionJson,
    string ApprovalState,
    bool IsCurrent,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);

public sealed record XeniaAgentResponse(
    Guid AgentId,
    string AgentCode,
    string DisplayName,
    string Description,
    bool Enabled,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);

public sealed record XeniaAgentVersionResponse(
    Guid AgentVersionId,
    Guid AgentId,
    int VersionNumber,
    string DefinitionJson,
    string ApprovalState,
    bool IsCurrent,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);

public sealed record XeniaKnowledgeSourceResponse(
    Guid KnowledgeSourceId,
    Guid? TenantId,
    string SourceCode,
    string DisplayName,
    string SourceType,
    string Status,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);

public sealed record XeniaMarketplaceAssetResponse(
    Guid MarketplaceAssetId,
    string AssetCode,
    string AssetType,
    string DisplayName,
    string Description,
    bool Enabled,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);

public sealed record XeniaMarketplaceInstallationResponse(
    Guid MarketplaceInstallationId,
    Guid MarketplaceAssetId,
    Guid TenantId,
    string Status,
    DateTime InstalledAtUtc,
    DateTime UpdatedAtUtc);
