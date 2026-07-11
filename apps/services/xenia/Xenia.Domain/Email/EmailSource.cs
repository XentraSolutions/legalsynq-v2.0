using Xenia.Domain.Common;

namespace Xenia.Domain.Email;

/// <summary>
/// Tenant-scoped email source configuration.
///
/// An email source represents a mailbox connection that Xenia can access to
/// perform automation tasks. It never stores plaintext credentials — all secret
/// material is represented by a <see cref="SecretReferenceId"/> that is resolved
/// at runtime by <c>ISecretReferenceService</c>.
///
/// Owned exclusively by Xenia — no other service shares this table.
/// </summary>
public sealed class EmailSource : AuditableEntityBase
{
    public const int DisplayNameMaxLength = 200;
    public const int DescriptionMaxLength = 1000;
    public const int EmailAddressMaxLength = 320;
    public const int UsernameMaxLength = 255;
    public const int HostMaxLength = 255;
    public const int FolderMaxLength = 255;
    public const int SecretRefMaxLength = 500;
    public const int ErrorCodeMaxLength = 100;
    public const int ErrorSummaryMaxLength = 500;
    public const string ModuleKeyValue = "email";

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }

    /// <summary>Always "email" — links this source to the Email module.</summary>
    public string ModuleKey { get; private set; } = ModuleKeyValue;

    public string DisplayName { get; private set; } = string.Empty;
    public string? Description { get; private set; }

    public EmailProviderType ProviderType { get; private set; }
    public EmailAuthType AuthType { get; private set; }

    /// <summary>The email address this source represents. Never a credential.</summary>
    public string EmailAddress { get; private set; } = string.Empty;

    /// <summary>Non-secret username for IMAP/POP3 where username differs from email address.</summary>
    public string? Username { get; private set; }

    /// <summary>Incoming mail server host (IMAP/POP3/ExchangeIMAP). Null for API-based providers.</summary>
    public string? IncomingHost { get; private set; }

    /// <summary>Incoming server port. Null for API-based providers.</summary>
    public int? IncomingPort { get; private set; }

    /// <summary>Whether TLS must be used. Defaults to true.</summary>
    public bool UseTls { get; private set; } = true;

    /// <summary>Target mailbox folder or label (e.g. "INBOX"). Null = default inbox.</summary>
    public string? MailboxFolder { get; private set; }

    /// <summary>
    /// Opaque reference to the secret that holds the credential.
    /// Must NEVER contain the actual password, token, or key.
    /// Resolved at runtime by ISecretReferenceService.
    /// </summary>
    public string? SecretReferenceId { get; private set; }

    /// <summary>
    /// Opaque reference to the OAuth connection state (token endpoint, client_id, etc.)
    /// stored in the secret service. Never contains actual tokens.
    /// </summary>
    public string? OAuthConnectionRef { get; private set; }

    public bool Enabled { get; private set; }
    public EmailSourceStatus Status { get; private set; }
    public EmailHealthStatus HealthStatus { get; private set; }
    public EmailValidationStatus ValidationStatus { get; private set; }

    public DateTime? LastValidatedAt { get; private set; }
    public DateTime? LastSuccessfulValidationAt { get; private set; }
    public int? LastValidationLatencyMs { get; private set; }
    public DateTime? LastConnectionAt { get; private set; }

    public string? LastErrorCode { get; private set; }
    public string? LastErrorSummary { get; private set; }

    public Guid? CreatedBy { get; private set; }
    public Guid? UpdatedBy { get; private set; }

    /// <summary>Optimistic concurrency token. Incremented on each update.</summary>
    public int RowVersion { get; private set; }

    // ── Soft delete ───────────────────────────────────────────────────────────
    /// <summary>True when the source has been soft-deleted. Excluded from all normal queries.</summary>
    public bool IsDeleted { get; private set; }
    public DateTime? DeletedAt { get; private set; }
    public Guid? DeletedBy { get; private set; }

    private EmailSource() { }

    public EmailSource(
        Guid id,
        Guid tenantId,
        string displayName,
        string emailAddress,
        EmailProviderType providerType,
        EmailAuthType authType,
        Guid? createdBy = null)
    {
        if (tenantId == Guid.Empty)
            throw new ArgumentException("TenantId cannot be empty.", nameof(tenantId));
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        ArgumentException.ThrowIfNullOrWhiteSpace(emailAddress);

        Id = id;
        TenantId = tenantId;
        DisplayName = displayName.Trim();
        EmailAddress = emailAddress.Trim().ToLowerInvariant();
        ProviderType = providerType;
        AuthType = authType;
        Enabled = true;
        Status = EmailSourceStatus.Pending;
        HealthStatus = EmailHealthStatus.Unknown;
        ValidationStatus = EmailValidationStatus.NotValidated;
        RowVersion = 1;
        CreatedBy = createdBy;
        UpdatedBy = createdBy;
    }

    public void UpdateDisplayName(string displayName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        DisplayName = displayName.Trim();
    }

    public void SetDescription(string? description) =>
        Description = description?.Trim();

    public void SetConnectionDetails(
        string? incomingHost,
        int? incomingPort,
        bool useTls,
        string? mailboxFolder,
        string? username)
    {
        IncomingHost = incomingHost?.Trim();
        IncomingPort = incomingPort;
        UseTls = useTls;
        MailboxFolder = mailboxFolder?.Trim();
        Username = username?.Trim();
    }

    /// <summary>
    /// Stores only the opaque reference identifier, never the secret value.
    /// Callers must validate that the value does not contain plaintext credentials.
    /// </summary>
    public void SetSecretReference(string? referenceId) =>
        SecretReferenceId = referenceId?.Trim();

    public void SetOAuthConnectionRef(string? oauthRef) =>
        OAuthConnectionRef = oauthRef?.Trim();

    public void Enable(Guid? actorId = null)
    {
        if (IsDeleted) throw new InvalidOperationException("Cannot enable a deleted email source.");
        Enabled = true;
        Status = EmailSourceStatus.Active;
        UpdatedBy = actorId;
        IncrementVersion();
    }

    public void Disable(Guid? actorId = null)
    {
        if (IsDeleted) throw new InvalidOperationException("Cannot disable a deleted email source.");
        Enabled = false;
        Status = EmailSourceStatus.Disabled;
        UpdatedBy = actorId;
        IncrementVersion();
    }

    /// <summary>
    /// Soft-deletes the source. Irreversible via normal APIs.
    /// Validation history and audit records are retained.
    /// </summary>
    public void SoftDelete(Guid? actorId = null)
    {
        if (IsDeleted) return; // idempotent
        IsDeleted = true;
        DeletedAt = DateTime.UtcNow;
        DeletedBy = actorId;
        Enabled = false;
        Status = EmailSourceStatus.Disabled;
        UpdatedBy = actorId;
        IncrementVersion();
    }

    public void RecordValidationStarted(Guid? actorId = null)
    {
        if (IsDeleted) throw new InvalidOperationException("Cannot validate a deleted email source.");
        Status = EmailSourceStatus.Validating;
        ValidationStatus = EmailValidationStatus.Pending;
        UpdatedBy = actorId;
    }

    public void RecordValidationSuccess(int latencyMs, Guid? actorId = null)
    {
        var now = DateTime.UtcNow;
        ValidationStatus = EmailValidationStatus.Valid;
        HealthStatus = EmailHealthStatus.Healthy;
        Status = Enabled ? EmailSourceStatus.Active : EmailSourceStatus.Disabled;
        LastValidatedAt = now;
        LastSuccessfulValidationAt = now;
        LastValidationLatencyMs = latencyMs;
        LastConnectionAt = now;
        LastErrorCode = null;
        LastErrorSummary = null;
        UpdatedBy = actorId;
        IncrementVersion();
    }

    public void RecordValidationFailure(
        string errorCode,
        string errorSummary,
        int latencyMs,
        Guid? actorId = null)
    {
        ValidationStatus = EmailValidationStatus.Invalid;
        HealthStatus = EmailHealthStatus.Unavailable;
        Status = EmailSourceStatus.Error;
        LastValidatedAt = DateTime.UtcNow;
        LastValidationLatencyMs = latencyMs;
        LastErrorCode = errorCode?.Length > ErrorCodeMaxLength
            ? errorCode[..ErrorCodeMaxLength] : errorCode;
        LastErrorSummary = errorSummary?.Length > ErrorSummaryMaxLength
            ? errorSummary[..ErrorSummaryMaxLength] : errorSummary;
        UpdatedBy = actorId;
        IncrementVersion();
    }

    public void SetUpdatedBy(Guid? actorId)
    {
        UpdatedBy = actorId;
        IncrementVersion();
    }

    private void IncrementVersion() => RowVersion++;
}
