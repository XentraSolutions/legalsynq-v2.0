using Xenia.Domain.Email;

namespace Xenia.Application.Email.Ingestion;

/// <summary>
/// The result of normalizing a <see cref="ProviderMessageEnvelope"/> into canonical form.
/// All fields have been sanitized, sized, and verified before this record is created.
/// </summary>
public sealed record NormalizedMessage
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
    public string? ReplyToAddressesCsv { get; init; }

    public DateTime? SentAt { get; init; }
    public DateTime? ReceivedAt { get; init; }

    public EmailImportance Importance { get; init; }
    public bool? IsRead { get; init; }
    public bool HasAttachments { get; init; }
    public int AttachmentCount { get; init; }

    public EmailMessageBodyType BodyType { get; init; }
    public string? BodyText { get; init; }
    public string? BodyHtml { get; init; }
    public string? BodyPreview { get; init; }

    public string? HeadersJson { get; init; }
    public string? ProviderMetadataJson { get; init; }
    public string? ContentHash { get; init; }

    public IReadOnlyList<NormalizedRecipient> Recipients { get; init; } = [];
    public IReadOnlyList<ProviderAttachmentDescriptor> Attachments { get; init; } = [];
}

/// <summary>Normalized recipient after sanitization.</summary>
public sealed record NormalizedRecipient(
    EmailRecipientType RecipientType,
    string EmailAddress,
    string? DisplayName);
