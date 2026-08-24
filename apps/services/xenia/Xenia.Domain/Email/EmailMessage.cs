using Xenia.Domain.Common;

namespace Xenia.Domain.Email;

/// <summary>
/// Canonical, provider-neutral email message aggregate.
///
/// Represents a single email message ingested from a tenant-configured Email source.
/// Provider-specific fields are stored in <see cref="ProviderMetadataJson"/>.
///
/// Rules:
/// - No attachment binary content is stored here.
/// - No credentials are stored here.
/// - Message body fields MUST NOT appear in logs or audit payloads.
/// - HTML content must be treated as untrusted when rendered.
/// - Headers are sanitized before persistence (sensitive headers removed; size capped).
/// - Content hash is stable across providers for the same logical message.
/// </summary>
public sealed class EmailMessage : AuditableEntityBase
{
    public const int SubjectMaxLength             = 998;
    public const int AddressMaxLength             = 320;
    public const int DisplayNameMaxLength         = 500;
    public const int ReplyToAddressesMaxLength    = 2000;
    public const int ProviderMessageIdMaxLength   = 1024;
    public const int InternetMessageIdMaxLength   = 998;
    public const int ThreadIdMaxLength            = 500;
    public const int ConversationIdMaxLength      = 500;
    public const int ContentHashMaxLength         = 128;
    public const int BodyPreviewMaxLength         = 500;
    public const int ErrorCodeMaxLength           = 100;
    public const int ErrorSummaryMaxLength        = 500;
    public const int ProviderMetadataMaxLength    = 8000;
    public const int HeadersJsonMaxLength         = 16000;

    // ── Identity ──────────────────────────────────────────────────────────────

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid EmailSourceId { get; private set; }

    /// <summary>Provider type that produced this message (denormalized for efficient filtering).</summary>
    public EmailProviderType ProviderType { get; private set; }

    // ── Provider message identity ─────────────────────────────────────────────

    /// <summary>Provider-specific unique message ID (e.g. Graph message ID, IMAP UID string, Gmail message ID).</summary>
    public string ProviderMessageId { get; private set; } = string.Empty;

    /// <summary>RFC 5322 Message-ID header value. Not guaranteed unique across all providers/tenants.</summary>
    public string? InternetMessageId { get; private set; }

    /// <summary>Provider thread/conversation thread identifier.</summary>
    public string? ThreadId { get; private set; }

    /// <summary>Provider conversation identifier (may differ from ThreadId on some providers).</summary>
    public string? ConversationId { get; private set; }

    // ── Addressing ────────────────────────────────────────────────────────────

    public string? Subject { get; private set; }
    public string? FromAddress { get; private set; }
    public string? FromName { get; private set; }

    /// <summary>Sender address (when Sender != From).</summary>
    public string? SenderAddress { get; private set; }
    public string? SenderName { get; private set; }

    /// <summary>CSV of reply-to addresses (safe representation; no binary).</summary>
    public string? ReplyToAddresses { get; private set; }

    // ── Timestamps ───────────────────────────────────────────────────────────

    public DateTime? SentAt { get; private set; }
    public DateTime? ReceivedAt { get; private set; }

    // ── Metadata ─────────────────────────────────────────────────────────────

    public EmailImportance Importance { get; private set; } = EmailImportance.Normal;
    public bool? IsRead { get; private set; }
    public bool HasAttachments { get; private set; }
    public int AttachmentCount { get; private set; }

    // ── Body ─────────────────────────────────────────────────────────────────

    public EmailMessageBodyType BodyType { get; private set; }

    /// <summary>Plain-text body. Size capped at configuration limit. MUST NOT be logged or audited.</summary>
    public string? BodyText { get; private set; }

    /// <summary>HTML body. Treated as UNTRUSTED content. MUST be sanitized before rendering. MUST NOT be logged or audited.</summary>
    public string? BodyHtml { get; private set; }

    /// <summary>Short safe preview derived from BodyText (first N chars, stripped of HTML). Safe to display in list views.</summary>
    public string? BodyPreview { get; private set; }

    // ── Headers and metadata ──────────────────────────────────────────────────

    /// <summary>Sanitized headers JSON. Sensitive headers removed. Size capped. Safe to store.</summary>
    public string? HeadersJson { get; private set; }

    /// <summary>Safe provider-specific metadata (no credentials, no raw tokens).</summary>
    public string? ProviderMetadataJson { get; private set; }

    /// <summary>SHA-256 hash of canonical message fields. Used as duplicate fallback signal.</summary>
    public string? ContentHash { get; private set; }

    // ── Import state ──────────────────────────────────────────────────────────

    public MessageImportStatus ImportStatus { get; private set; } = MessageImportStatus.Pending;
    public MessageProcessingState ProcessingState { get; private set; } = MessageProcessingState.Pending;

    public DateTime? ImportedAt { get; private set; }
    public DateTime? LastObservedAt { get; private set; }

    /// <summary>The ingestion run that last imported or updated this message.</summary>
    public Guid? LastIngestionRunId { get; private set; }

    /// <summary>Optimistic concurrency version.</summary>
    public int Version { get; private set; }

    // ── EF constructor ────────────────────────────────────────────────────────

    private EmailMessage() { }

    // ── Factory ───────────────────────────────────────────────────────────────

    public static EmailMessage Create(
        Guid tenantId,
        Guid emailSourceId,
        EmailProviderType providerType,
        string providerMessageId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(providerMessageId);

        return new EmailMessage
        {
            Id                = Guid.CreateVersion7(),
            TenantId          = tenantId,
            EmailSourceId     = emailSourceId,
            ProviderType      = providerType,
            ProviderMessageId = providerMessageId,
            ImportStatus      = MessageImportStatus.Pending,
            ProcessingState   = MessageProcessingState.Pending,
            Version           = 0,
        };
    }

    // ── Setters (called by normalizer) ────────────────────────────────────────

    public void SetAddressing(
        string? subject,
        string? fromAddress, string? fromName,
        string? senderAddress, string? senderName,
        string? replyToAddresses,
        string? internetMessageId, string? threadId, string? conversationId)
    {
        Subject           = subject;
        FromAddress       = fromAddress;
        FromName          = fromName;
        SenderAddress     = senderAddress;
        SenderName        = senderName;
        ReplyToAddresses  = replyToAddresses;
        InternetMessageId = internetMessageId;
        ThreadId          = threadId;
        ConversationId    = conversationId;
    }

    public void SetTimestamps(DateTime? sentAt, DateTime? receivedAt)
    {
        SentAt     = sentAt.HasValue ? DateTime.SpecifyKind(sentAt.Value, DateTimeKind.Utc) : null;
        ReceivedAt = receivedAt.HasValue ? DateTime.SpecifyKind(receivedAt.Value, DateTimeKind.Utc) : null;
    }

    public void SetMetadata(EmailImportance importance, bool? isRead, bool hasAttachments, int attachmentCount)
    {
        Importance       = importance;
        IsRead           = isRead;
        HasAttachments   = hasAttachments;
        AttachmentCount  = attachmentCount;
    }

    public void SetBody(EmailMessageBodyType bodyType, string? bodyText, string? bodyHtml, string? bodyPreview)
    {
        BodyType    = bodyType;
        BodyText    = bodyText;
        BodyHtml    = bodyHtml;
        BodyPreview = bodyPreview;
    }

    public void SetHeadersAndMetadata(string? headersJson, string? providerMetadataJson, string? contentHash)
    {
        HeadersJson           = headersJson;
        ProviderMetadataJson  = providerMetadataJson;
        ContentHash           = contentHash;
    }

    public void MarkImported(Guid runId)
    {
        ImportStatus       = MessageImportStatus.Imported;
        ImportedAt         = DateTime.UtcNow;
        LastObservedAt     = DateTime.UtcNow;
        LastIngestionRunId = runId;
        Version++;
    }

    public void MarkUpdated(Guid runId)
    {
        ImportStatus       = MessageImportStatus.Updated;
        LastObservedAt     = DateTime.UtcNow;
        LastIngestionRunId = runId;
        Version++;
    }

    public void MarkDuplicate(Guid runId)
    {
        ImportStatus       = MessageImportStatus.Duplicate;
        LastObservedAt     = DateTime.UtcNow;
        LastIngestionRunId = runId;
    }

    public void SetProcessingState(MessageProcessingState state) => ProcessingState = state;
}
