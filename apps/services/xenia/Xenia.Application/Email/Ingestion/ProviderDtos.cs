using Xenia.Domain.Email;

namespace Xenia.Application.Email.Ingestion;

/// <summary>
/// Provider-neutral envelope for a single message fetched from a mail provider.
/// All fields are safe values — no raw tokens, no credentials.
/// </summary>
public sealed record ProviderMessageEnvelope
{
    public required string ProviderMessageId { get; init; }
    public string? InternetMessageId { get; init; }
    public string? ThreadId { get; init; }
    public string? ConversationId { get; init; }

    public string? Subject { get; init; }
    public string? FromAddress { get; init; }
    public string? FromName { get; init; }
    public string? SenderAddress { get; init; }
    public string? SenderName { get; init; }

    public IReadOnlyList<ProviderRecipient> To { get; init; } = [];
    public IReadOnlyList<ProviderRecipient> Cc { get; init; } = [];
    public IReadOnlyList<ProviderRecipient> Bcc { get; init; } = [];
    public IReadOnlyList<ProviderRecipient> ReplyTo { get; init; } = [];

    public DateTime? SentAt { get; init; }
    public DateTime? ReceivedAt { get; init; }

    public EmailImportance Importance { get; init; } = EmailImportance.Normal;
    public bool? IsRead { get; init; }

    public string? BodyText { get; init; }
    public string? BodyHtml { get; init; }

    /// <summary>Raw headers (unsanitized — normalizer will sanitize).</summary>
    public IReadOnlyDictionary<string, string> Headers { get; init; } = new Dictionary<string, string>();

    public IReadOnlyList<ProviderAttachmentDescriptor> Attachments { get; init; } = [];

    /// <summary>Provider-specific metadata bag (no secrets). Safe to serialize.</summary>
    public IReadOnlyDictionary<string, string> ProviderMetadata { get; init; } = new Dictionary<string, string>();
}

/// <summary>A single recipient as returned by the provider.</summary>
public sealed record ProviderRecipient(string EmailAddress, string? DisplayName);

/// <summary>Descriptor for an attachment discovered in a provider message.</summary>
public sealed record ProviderAttachmentDescriptor
{
    public required string ProviderAttachmentId { get; init; }
    public required string FileName { get; init; }
    public string? MimeType { get; init; }
    public long? SizeBytes { get; init; }
    public bool IsInline { get; init; }
    public string? ContentId { get; init; }
    public string? Disposition { get; init; }
}

/// <summary>
/// A page of messages returned by the provider connector.
/// </summary>
public sealed record ProviderSyncPage
{
    public required IReadOnlyList<ProviderMessageEnvelope> Messages { get; init; }

    /// <summary>The cursor to pass for the NEXT page. Null means end of pages.</summary>
    public ProviderSyncCursor? NextCursor { get; init; }

    /// <summary>Whether there are more pages after this one.</summary>
    public bool HasMore => NextCursor is not null;

    /// <summary>Provider-reported rate-limit retry delay, if any.</summary>
    public TimeSpan? RetryAfter { get; init; }

    public bool WasRateLimited { get; init; }
}

/// <summary>
/// Opaque cursor identifying a position in the provider message stream.
/// The RawValue is NEVER exposed in APIs, logs, or audit events.
/// Use SafeSummary for display purposes.
/// </summary>
public sealed record ProviderSyncCursor
{
    public required SyncCursorType CursorType { get; init; }
    public required string RawValue { get; init; }
    public string? MetadataJson { get; init; }
    public string? SafeSummary { get; init; }
}

/// <summary>Capabilities of the provider ingestion connector in the current environment.</summary>
public sealed record ProviderSyncCapabilities
{
    public required EmailProviderType ProviderType { get; init; }
    public required bool CanFetchMessages { get; init; }
    public required bool CanFetchAttachments { get; init; }
    public required bool SupportsDeltaSync { get; init; }
    public required bool SupportsCancel { get; init; }
    public string? UnavailableReason { get; init; }
}

/// <summary>Result of attempting to get the initial cursor for a source.</summary>
public sealed record ProviderInitialCursorResult
{
    public required bool Success { get; init; }
    public ProviderSyncCursor? Cursor { get; init; }
    public string? ErrorCode { get; init; }
    public string? SafeErrorSummary { get; init; }
}

/// <summary>Result of a single page fetch attempt.</summary>
public sealed record ProviderFetchPageResult
{
    public required bool Success { get; init; }
    public ProviderSyncPage? Page { get; init; }
    public string? ErrorCode { get; init; }
    public string? SafeErrorSummary { get; init; }
    public TimeSpan? RetryAfter { get; init; }
    public bool IsInvalidCursor { get; init; }
    public bool IsAuthFailure { get; init; }
    public bool IsRateLimited { get; init; }
    public bool IsProviderTimeout { get; init; }
}
