namespace Notifications.Application.DTOs;

// ── Query model ───────────────────────────────────────────────────────────────

/// <summary>
/// LS-NOTIF-SMS-006: Bounded query parameters for SMS activity log APIs.
/// All filters are optional; omitted filters are not applied.
/// </summary>
public sealed class SmsActivityQuery
{
    /// <summary>
    /// Tenant scope. Set from JWT tenant_id for tenant callers.
    /// Null in admin mode means cross-tenant (all tenants).
    /// </summary>
    public Guid? TenantId { get; set; }

    /// <summary>
    /// Admin-only: when true, include attempts where ProviderOwnershipMode = "platform".
    /// When false (default), platform-owned attempts are excluded from results.
    /// </summary>
    public bool IncludePlatformActivity { get; set; } = false;

    /// <summary>Filter by provider name (e.g. "twilio").</summary>
    public string? Provider { get; set; }

    /// <summary>Filter by the tenant provider config used for the send.</summary>
    public Guid? ProviderConfigId { get; set; }

    /// <summary>Filter by ownership mode: "tenant" | "platform".</summary>
    public string? ProviderOwnershipMode { get; set; }

    /// <summary>Filter by provider message ID (Twilio MessageSid).</summary>
    public string? ProviderMessageId { get; set; }

    /// <summary>Filter by attempt status (e.g. "sent", "delivered", "failed").</summary>
    public string? Status { get; set; }

    /// <summary>Filter by failure category code.</summary>
    public string? FailureCategory { get; set; }

    /// <summary>Inclusive start of the CreatedAt window (UTC).</summary>
    public DateTime? FromDate { get; set; }

    /// <summary>Inclusive end of the CreatedAt window (UTC).</summary>
    public DateTime? ToDate { get; set; }

    /// <summary>Page size. Capped at 200 by the API layer.</summary>
    public int Limit { get; set; } = 50;

    /// <summary>Zero-based row offset for pagination.</summary>
    public int Offset { get; set; } = 0;
}

// ── Raw record returned by repository (internal Application layer type) ───────

/// <summary>
/// Intermediate projection from the repository JOIN between
/// ntf_NotificationAttempts and ntf_Notifications.
/// RecipientJson is included for phone masking at the service layer and
/// MUST NOT be exposed in API responses.
/// </summary>
public sealed record SmsActivityRawRecord(
    Guid AttemptId,
    Guid NotificationId,
    Guid? TenantId,
    string Provider,
    Guid? ProviderConfigId,
    string? ProviderOwnershipMode,
    string? ProviderMessageId,
    string Status,
    string? FailureCategory,
    string? ErrorMessage,
    bool IsFailover,
    int AttemptNumber,
    DateTime? CompletedAt,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    /// <summary>Raw recipient JSON from ntf_Notifications — for masking only.</summary>
    string? RecipientJson);

// ── Response DTOs ─────────────────────────────────────────────────────────────

/// <summary>
/// LS-NOTIF-SMS-006: A single outbound SMS activity record.
/// Safe for external API responses — no credentials, no raw phone numbers.
/// </summary>
public sealed class SmsActivityItemDto
{
    public Guid AttemptId { get; init; }
    public Guid NotificationId { get; init; }
    public Guid? TenantId { get; init; }

    /// <summary>Always "sms".</summary>
    public string Channel { get; init; } = "sms";

    public string Provider { get; init; } = string.Empty;

    /// <summary>
    /// Opaque ID of the tenant provider config used (if tenant-managed).
    /// Null for platform-managed sends.
    /// Never returns CredentialsJson or SettingsJson.
    /// </summary>
    public Guid? ProviderConfigId { get; init; }

    /// <summary>"tenant" | "platform" | "unknown".</summary>
    public string Attribution { get; init; } = "unknown";

    /// <summary>Raw ownership mode string for filtering. Null if not set.</summary>
    public string? ProviderOwnershipMode { get; init; }

    /// <summary>Twilio MessageSid — operational metadata, not a secret.</summary>
    public string? ProviderMessageId { get; init; }

    public string Status { get; init; } = string.Empty;
    public string? FailureCategory { get; init; }

    /// <summary>Safe last error description. Never includes credentials.</summary>
    public string? LastError { get; init; }

    /// <summary>
    /// Masked recipient phone number following platform convention:
    /// first 3 characters + "***" (e.g. "+1***").
    /// </summary>
    public string? MaskedRecipient { get; init; }

    public bool IsFailover { get; init; }
    public int AttemptNumber { get; init; }

    /// <summary>
    /// When the attempt reached a terminal state (sent/delivered/failed/dead_letter).
    /// Null for still-pending attempts.
    /// </summary>
    public DateTime? CompletedAt { get; init; }

    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
}

/// <summary>
/// LS-NOTIF-SMS-006: Aggregate counts for the filtered SMS activity window.
/// </summary>
public sealed class SmsActivitySummaryDto
{
    public int Total { get; init; }
    public int Sent { get; init; }
    public int Delivered { get; init; }
    public int Failed { get; init; }
    public int DeadLetter { get; init; }

    /// <summary>pending + sending + queued + processing + retrying</summary>
    public int InProgress { get; init; }

    public int TenantOwned { get; init; }
    public int PlatformOwned { get; init; }
    public int UnknownAttribution { get; init; }

    /// <summary>Earliest CreatedAt in the result window. Null if no records.</summary>
    public DateTime? EarliestAt { get; init; }

    /// <summary>Latest CreatedAt in the result window. Null if no records.</summary>
    public DateTime? LatestAt { get; init; }
}

/// <summary>
/// LS-NOTIF-SMS-006: Paginated SMS activity response.
/// </summary>
public sealed class SmsActivityPagedResult
{
    public IReadOnlyList<SmsActivityItemDto> Items { get; init; } = Array.Empty<SmsActivityItemDto>();
    public int Total { get; init; }
    public int Limit { get; init; }
    public int Offset { get; init; }
}
