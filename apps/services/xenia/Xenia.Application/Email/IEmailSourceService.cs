using Xenia.Domain.Email;

namespace Xenia.Application.Email;

/// <summary>
/// Application-layer service for managing tenant-scoped email sources.
///
/// All methods enforce tenant isolation from the JWT context.
/// No method accepts a caller-supplied tenant ID — tenant context
/// is resolved from <c>IXeniaTenantContext</c> only.
/// </summary>
public interface IEmailSourceService
{
    /// <summary>Returns all email sources for the current tenant.</summary>
    Task<IReadOnlyList<EmailSourceDto>> GetSourcesAsync(
        Guid tenantId,
        CancellationToken ct = default);

    /// <summary>
    /// Returns a single email source by ID.
    /// Returns null if the source does not exist or belongs to a different tenant.
    /// </summary>
    Task<EmailSourceDto?> GetSourceAsync(Guid tenantId, Guid sourceId, CancellationToken ct = default);

    /// <summary>Creates a new email source for the tenant.</summary>
    Task<EmailSourceDto> CreateSourceAsync(
        Guid tenantId,
        Guid? actorId,
        CreateEmailSourceRequest request,
        CancellationToken ct = default);

    /// <summary>
    /// Updates an existing email source.
    /// Returns null if the source is not found or belongs to a different tenant.
    /// </summary>
    Task<EmailSourceDto?> UpdateSourceAsync(
        Guid tenantId,
        Guid sourceId,
        Guid? actorId,
        UpdateEmailSourceRequest request,
        CancellationToken ct = default);

    /// <summary>
    /// Deletes an email source. Returns false if not found or cross-tenant.
    /// </summary>
    Task<bool> DeleteSourceAsync(Guid tenantId, Guid sourceId, Guid? actorId, CancellationToken ct = default);

    /// <summary>Enables a source. Returns false if not found or cross-tenant.</summary>
    Task<bool> EnableSourceAsync(Guid tenantId, Guid sourceId, Guid? actorId, CancellationToken ct = default);

    /// <summary>Disables a source. Returns false if not found or cross-tenant.</summary>
    Task<bool> DisableSourceAsync(Guid tenantId, Guid sourceId, Guid? actorId, CancellationToken ct = default);

    /// <summary>
    /// Tests connectivity for a source and records the result.
    /// Returns a validation result DTO — never throws.
    /// </summary>
    Task<EmailValidationResultDto> ValidateSourceAsync(
        Guid tenantId,
        Guid sourceId,
        Guid? actorId,
        string? correlationId,
        CancellationToken ct = default);

    /// <summary>Returns validation history for a source, newest first.</summary>
    Task<IReadOnlyList<ValidationHistoryDto>> GetValidationHistoryAsync(
        Guid tenantId,
        Guid sourceId,
        int limit,
        CancellationToken ct = default);
}

// ── DTOs ─────────────────────────────────────────────────────────────────────

/// <summary>Safe read model for an email source. Never exposes credentials.</summary>
public sealed record EmailSourceDto
{
    public required Guid Id { get; init; }
    public required Guid TenantId { get; init; }
    public required string ModuleKey { get; init; }
    public required string DisplayName { get; init; }
    public string? Description { get; init; }
    public required string ProviderType { get; init; }
    public required string AuthType { get; init; }
    public required string EmailAddress { get; init; }
    public string? Username { get; init; }
    public string? IncomingHost { get; init; }
    public int? IncomingPort { get; init; }
    public required bool UseTls { get; init; }
    public string? MailboxFolder { get; init; }

    /// <summary>Indicates whether a secret reference is configured. Does not expose the reference ID.</summary>
    public required bool HasSecretReference { get; init; }

    /// <summary>Indicates whether an OAuth connection reference is configured.</summary>
    public required bool HasOAuthConnection { get; init; }

    public required bool Enabled { get; init; }
    public required string Status { get; init; }
    public required string HealthStatus { get; init; }
    public required string ValidationStatus { get; init; }
    public DateTime? LastValidatedAt { get; init; }
    public DateTime? LastSuccessfulValidationAt { get; init; }
    public int? LastValidationLatencyMs { get; init; }
    public DateTime? LastConnectionAt { get; init; }
    public string? LastErrorCode { get; init; }
    public string? LastErrorSummary { get; init; }
    public required int RowVersion { get; init; }
    public required DateTime CreatedAtUtc { get; init; }
    public required DateTime UpdatedAtUtc { get; init; }

    public static EmailSourceDto FromEntity(EmailSource s) => new()
    {
        Id = s.Id,
        TenantId = s.TenantId,
        ModuleKey = s.ModuleKey,
        DisplayName = s.DisplayName,
        Description = s.Description,
        ProviderType = s.ProviderType.ToString(),
        AuthType = s.AuthType.ToString(),
        EmailAddress = s.EmailAddress,
        Username = s.Username,
        IncomingHost = s.IncomingHost,
        IncomingPort = s.IncomingPort,
        UseTls = s.UseTls,
        MailboxFolder = s.MailboxFolder,
        HasSecretReference = s.SecretReferenceId is not null,
        HasOAuthConnection = s.OAuthConnectionRef is not null,
        Enabled = s.Enabled,
        Status = s.Status.ToString(),
        HealthStatus = s.HealthStatus.ToString(),
        ValidationStatus = s.ValidationStatus.ToString(),
        LastValidatedAt = s.LastValidatedAt,
        LastSuccessfulValidationAt = s.LastSuccessfulValidationAt,
        LastValidationLatencyMs = s.LastValidationLatencyMs,
        LastConnectionAt = s.LastConnectionAt,
        LastErrorCode = s.LastErrorCode,
        LastErrorSummary = s.LastErrorSummary,
        RowVersion = s.RowVersion,
        CreatedAtUtc = s.CreatedAtUtc,
        UpdatedAtUtc = s.UpdatedAtUtc,
    };
}

public sealed record CreateEmailSourceRequest
{
    public required string DisplayName { get; init; }
    public string? Description { get; init; }
    public required string ProviderType { get; init; }
    public required string AuthType { get; init; }
    public required string EmailAddress { get; init; }
    public string? Username { get; init; }
    public string? IncomingHost { get; init; }
    public int? IncomingPort { get; init; }
    public bool UseTls { get; init; } = true;
    public string? MailboxFolder { get; init; }
    public string? SecretReferenceId { get; init; }
    public string? OAuthConnectionRef { get; init; }
    public bool Enabled { get; init; } = true;
    public string? ProviderConfigurationJson { get; init; }
}

public sealed record UpdateEmailSourceRequest
{
    public string? DisplayName { get; init; }
    public string? Description { get; init; }
    public string? Username { get; init; }
    public string? IncomingHost { get; init; }
    public int? IncomingPort { get; init; }
    public bool? UseTls { get; init; }
    public string? MailboxFolder { get; init; }
    public string? SecretReferenceId { get; init; }
    public string? OAuthConnectionRef { get; init; }
    public string? ProviderConfigurationJson { get; init; }
    public int? ExpectedRowVersion { get; init; }
}

public sealed record EmailValidationResultDto
{
    public required Guid SourceId { get; init; }
    public required bool Success { get; init; }
    public required string Result { get; init; }
    public required int DurationMs { get; init; }
    public string? ErrorCode { get; init; }
    public string? SafeErrorSummary { get; init; }
    public required DateTime ValidatedAt { get; init; }
}

public sealed record ValidationHistoryDto
{
    public required Guid Id { get; init; }
    public required Guid EmailSourceId { get; init; }
    public required string ProviderType { get; init; }
    public required string ValidationType { get; init; }
    public required DateTime StartedAt { get; init; }
    public DateTime? CompletedAt { get; init; }
    public int? DurationMs { get; init; }
    public required string Result { get; init; }
    public string? ErrorCode { get; init; }
    public string? ErrorSummary { get; init; }
    public required DateTime CreatedAtUtc { get; init; }
}

/// <summary>Provider definition returned by the catalog API. Safe for UI display.</summary>
public sealed record EmailProviderDefinitionDto
{
    public required string ProviderKey { get; init; }
    public required string DisplayName { get; init; }
    public required string Category { get; init; }
    public required IReadOnlyList<string> SupportedAuthTypes { get; init; }
    public string? DefaultIncomingHost { get; init; }
    public int? DefaultPort { get; init; }
    public required bool RequiresTls { get; init; }
    public required bool SupportsOAuth { get; init; }
    public required bool SupportsUsernamePassword { get; init; }
    public required bool ValidationAvailable { get; init; }
    public string? HelpText { get; init; }
}
