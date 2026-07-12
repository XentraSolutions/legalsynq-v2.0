using Xenia.Domain;

namespace Xenia.Application;

public sealed record XeniaProviderCredentialRecord(
    Guid ProviderCredentialId,
    Guid ProviderConfigurationId,
    XeniaCredentialStorageMode StorageMode,
    string Fingerprint,
    string? LastFour,
    DateTime? LastVerifiedAtUtc,
    XeniaVerificationStatus VerificationStatus,
    bool IsActive,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);

public sealed record XeniaResolvedCredential(
    XeniaCredentialStorageMode StorageMode,
    string Secret,
    string Fingerprint,
    string? LastFour);

public sealed record XeniaAiExecutionContext(
    Guid TenantId,
    string ActorUserId,
    string ProductCode,
    string Prompt,
    string ActivationSource,
    string RequestKind,
    IReadOnlyDictionary<string, string>? Context,
    string? ActionLabel,
    string? AuditClassification,
    string? ApplyOutcome,
    IReadOnlyList<string>? SourceObjectReferences);

public sealed record XeniaAiResponse(
    string Provider,
    string Model,
    string Output,
    IReadOnlyList<string> OutputChunks,
    int PromptTokens,
    int CompletionTokens,
    decimal EstimatedCostUsd,
    long DurationMs,
    bool UsedProviderExecution,
    string? ErrorCode = null);

public sealed record XeniaProviderValidationResult(
    bool Success,
    string Status,
    string Message,
    DateTime VerifiedAtUtc,
    string? Fingerprint);

public interface IAiProviderGateway
{
    XeniaAiResponse Execute(XeniaProviderConfiguration provider, XeniaAiExecutionContext context);
    XeniaProviderValidationResult Validate(XeniaProviderConfiguration provider, XeniaResolvedCredential credential);
}

public interface IAiProviderAdapter
{
    bool CanHandle(XeniaProviderType providerType);
    XeniaAiResponse Execute(XeniaProviderConfiguration provider, XeniaResolvedCredential credential, XeniaAiExecutionContext context);
    XeniaProviderValidationResult Validate(XeniaProviderConfiguration provider, XeniaResolvedCredential credential);
}

public interface IAiCredentialStore
{
    XeniaProviderCredentialRecord Save(Guid providerConfigurationId, XeniaCredentialStorageMode storageMode, string? apiKey, string? externalSecretReference);
    XeniaProviderCredentialRecord? GetActiveMetadata(Guid providerConfigurationId);
    XeniaResolvedCredential? Resolve(Guid providerConfigurationId, XeniaProviderConfiguration provider);
}

public interface IAiUsageNormalizer
{
    XeniaUsageEvent CreateUsageEvent(Guid tenantId, string userId, string eventKind, XeniaAiResponse response);
}

public interface IAiProviderHealthCheck
{
    XeniaProviderHealthEvent CreateHealthEvent(XeniaProviderConfiguration provider, XeniaProviderValidationResult result);
}

public interface IProviderRoutingPolicy
{
    XeniaProviderConfiguration Resolve(XeniaTenantConfiguration configuration, Guid tenantId, IReadOnlyList<XeniaProviderConfiguration> providers);
}

public interface IProviderFailoverPolicy
{
    XeniaProviderConfiguration SelectFallback(XeniaTenantConfiguration configuration, Guid tenantId, IReadOnlyList<XeniaProviderConfiguration> providers, Guid failedProviderConfigurationId);
}
