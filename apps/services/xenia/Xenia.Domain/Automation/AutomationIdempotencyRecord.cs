using Xenia.Domain.Common;

namespace Xenia.Domain.Automation;

/// <summary>
/// Durable idempotency record for deduplicating automation execution requests.
///
/// Rules:
/// - Unique per (TenantId, AutomationKey, IdempotencyKey).
/// - RequestFingerprint uses safe canonical request metadata — no raw payload.
/// - Configurable expiration via ExpiresAt.
/// - Purge-safe: expired records may be deleted without losing active executions.
/// - Cross-tenant same key: independent (tenant-scoped unique constraint).
/// </summary>
public sealed class AutomationIdempotencyRecord : AuditableEntityBase
{
    public const int AutomationKeyMaxLength        = 200;
    public const int IdempotencyKeyMaxLength       = 200;
    public const int RequestFingerprintMaxLength   = 64;

    private AutomationIdempotencyRecord() { }

    public Guid Id { get; private set; }

    public Guid TenantId { get; private set; }
    public string AutomationKey { get; private set; } = string.Empty;

    /// <summary>Client-supplied idempotency key.</summary>
    public string IdempotencyKey { get; private set; } = string.Empty;

    /// <summary>
    /// SHA-256 or equivalent fingerprint of safe canonical request metadata.
    /// Used to detect same-key, different-request conflicts.
    /// Must not contain raw request payload.
    /// </summary>
    public string RequestFingerprint { get; private set; } = string.Empty;

    /// <summary>Execution created for this idempotency key. Null if reservation is pending.</summary>
    public Guid? ExecutionId { get; private set; }

    /// <summary>When this record expires and may be purged.</summary>
    public DateTime ExpiresAt { get; private set; }

    public uint RowVersion { get; private set; }

    public static AutomationIdempotencyRecord Reserve(
        Guid tenantId,
        string automationKey,
        string idempotencyKey,
        string requestFingerprint,
        DateTime expiresAt)
    {
        return new AutomationIdempotencyRecord
        {
            Id                 = Guid.CreateVersion7(),
            TenantId           = tenantId,
            AutomationKey      = automationKey,
            IdempotencyKey     = idempotencyKey,
            RequestFingerprint = requestFingerprint,
            ExecutionId        = null,
            ExpiresAt          = expiresAt,
            RowVersion         = 0,
        };
    }

    public void BindExecution(Guid executionId)
    {
        ExecutionId = executionId;
        RowVersion++;
    }

    /// <summary>Returns true if this record's fingerprint matches the supplied fingerprint.</summary>
    public bool FingerprintMatches(string requestFingerprint)
        => string.Equals(RequestFingerprint, requestFingerprint, StringComparison.OrdinalIgnoreCase);
}
