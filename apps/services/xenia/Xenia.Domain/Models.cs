namespace Xenia.Domain;

public enum XeniaDeploymentModel
{
    Managed,
    BringYourOwnAI,
}

public enum XeniaProviderType
{
    OpenAI,
    Anthropic,
    Gemini,
    AzureOpenAI,
    AwsBedrock,
}

public enum XeniaProviderScope
{
    Platform,
    Tenant,
}

public enum XeniaCredentialStorageMode
{
    EncryptedDatabase,
    ExternalSecretReference,
}

public enum XeniaVerificationStatus
{
    Unverified,
    Verified,
    Failed,
}

public enum XeniaMessageRole
{
    System,
    User,
    Assistant,
}

public sealed class XeniaTenantConfiguration
{
    public Guid TenantId { get; set; }
    public bool Enabled { get; set; } = true;
    public XeniaDeploymentModel DeploymentModel { get; set; } = XeniaDeploymentModel.Managed;
    public Guid? DefaultProviderConfigurationId { get; set; }
    public string DefaultModel { get; set; } = "gpt-4.1-mini";
    public double Temperature { get; set; } = 0.2d;
    public int MaxTokens { get; set; } = 2000;
    public string ReasoningLevel { get; set; } = "Standard";
    public string RetentionPolicy { get; set; } = "TenantDefault";
    public string ModerationPolicy { get; set; } = "Standard";
    public bool FailoverEnabled { get; set; } = true;
    public Guid? BudgetPolicyId { get; set; }
    public Guid? QuotaPolicyId { get; set; }
    public List<string> AllowedSkills { get; set; } = [];
    public List<string> AllowedAgents { get; set; } = [];
    public List<string> AllowedTools { get; set; } = [];
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class XeniaProviderConfiguration
{
    public Guid ProviderConfigurationId { get; set; }
    public XeniaProviderType ProviderType { get; set; }
    public XeniaProviderScope Scope { get; set; } = XeniaProviderScope.Platform;
    public Guid? TenantId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string? Endpoint { get; set; }
    public string? Region { get; set; }
    public string? AzureDeploymentName { get; set; }
    public string DefaultModel { get; set; } = string.Empty;
    public List<string> AllowedModels { get; set; } = [];
    public int TimeoutSeconds { get; set; } = 60;
    public int RetryCount { get; set; } = 2;
    public int FailoverPriority { get; set; } = 100;
    public bool Enabled { get; set; } = true;
    public XeniaVerificationStatus VerificationStatus { get; set; } = XeniaVerificationStatus.Unverified;
    public DateTime? LastVerifiedAtUtc { get; set; }
    public XeniaCredentialStorageMode CredentialStorageMode { get; set; } = XeniaCredentialStorageMode.EncryptedDatabase;
    public string? SecretReference { get; set; }
    public string? CredentialFingerprint { get; set; }
    public string? CredentialLastFour { get; set; }
    public bool HasStoredCredential { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class XeniaProviderCredential
{
    public Guid ProviderCredentialId { get; set; }
    public Guid ProviderConfigurationId { get; set; }
    public XeniaCredentialStorageMode StorageMode { get; set; } = XeniaCredentialStorageMode.EncryptedDatabase;
    public string? EncryptedSecretPayload { get; set; }
    public string? ExternalSecretReference { get; set; }
    public string Fingerprint { get; set; } = string.Empty;
    public string? LastFour { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime? LastVerifiedAtUtc { get; set; }
    public XeniaVerificationStatus VerificationStatus { get; set; } = XeniaVerificationStatus.Unverified;
    public DateTime? RotatedAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class XeniaConversation
{
    public Guid ConversationId { get; set; }
    public Guid TenantId { get; set; }
    public string CreatedByUserId { get; set; } = string.Empty;
    public string Title { get; set; } = "Untitled Conversation";
    public string ActivationSource { get; set; } = "UserClick";
    public string? ProductCode { get; set; }
    public string? SourceReference { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public List<XeniaConversationMessage> Messages { get; set; } = [];
}

public sealed class XeniaConversationMessage
{
    public Guid MessageId { get; set; }
    public Guid ConversationId { get; set; }
    public XeniaMessageRole Role { get; set; } = XeniaMessageRole.User;
    public string Content { get; set; } = string.Empty;
    public string? ActionLabel { get; set; }
    public string? ProductCode { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class XeniaUsageEvent
{
    public Guid UsageEventId { get; set; }
    public Guid TenantId { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string EventKind { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public int PromptTokens { get; set; }
    public int CompletionTokens { get; set; }
    public decimal EstimatedCostUsd { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class XeniaAuditEvent
{
    public Guid AuditEventId { get; set; }
    public Guid TenantId { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string ActorUserId { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class XeniaProviderHealthEvent
{
    public Guid ProviderHealthEventId { get; set; }
    public Guid ProviderConfigurationId { get; set; }
    public string ProviderName { get; set; } = string.Empty;
    public string Status { get; set; } = "Healthy";
    public string Message { get; set; } = string.Empty;
    public DateTime CheckedAtUtc { get; set; } = DateTime.UtcNow;
}
