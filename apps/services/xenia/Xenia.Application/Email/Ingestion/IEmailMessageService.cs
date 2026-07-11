using Xenia.Domain.Email;

namespace Xenia.Application.Email.Ingestion;

/// <summary>Read-only query service for ingested email messages.</summary>
public interface IEmailMessageService
{
    Task<EmailMessagePage> ListMessagesAsync(EmailMessageQuery query, CancellationToken ct = default);
    Task<EmailMessageDetail?> GetMessageAsync(Guid tenantId, Guid messageId, CancellationToken ct = default);
    Task<IReadOnlyList<AttachmentReferenceDto>> GetAttachmentsAsync(Guid tenantId, Guid messageId, CancellationToken ct = default);
}

public sealed record EmailMessageQuery
{
    public required Guid TenantId { get; init; }
    public Guid? EmailSourceId { get; init; }
    public string? FromAddress { get; init; }
    public string? SubjectContains { get; init; }
    public MessageImportStatus? ImportStatus { get; init; }
    public bool? HasAttachments { get; init; }
    public DateTime? ReceivedAfter { get; init; }
    public DateTime? ReceivedBefore { get; init; }
    public int PageSize { get; init; } = 50;
    public int PageOffset { get; init; } = 0;
}

public sealed record EmailMessagePage
{
    public required IReadOnlyList<EmailMessageSummary> Messages { get; init; }
    public required int TotalCount { get; init; }
    public required int PageSize { get; init; }
    public required int PageOffset { get; init; }
}

public sealed record EmailMessageSummary
{
    public required Guid Id { get; init; }
    public required Guid TenantId { get; init; }
    public required Guid EmailSourceId { get; init; }
    public required string? Subject { get; init; }
    public required string? FromAddress { get; init; }
    public required string? FromName { get; init; }
    public required DateTime? ReceivedAt { get; init; }
    public required bool HasAttachments { get; init; }
    public required int AttachmentCount { get; init; }
    public required EmailImportance Importance { get; init; }
    public required bool? IsRead { get; init; }
    public required string? BodyPreview { get; init; }
    public required MessageImportStatus ImportStatus { get; init; }
    public required DateTime? ImportedAt { get; init; }
}

public sealed record EmailMessageDetail
{
    public required Guid Id { get; init; }
    public required Guid TenantId { get; init; }
    public required Guid EmailSourceId { get; init; }
    public required string? Subject { get; init; }
    public required string? FromAddress { get; init; }
    public required string? FromName { get; init; }
    public required string? SenderAddress { get; init; }
    public required IReadOnlyList<RecipientDto> Recipients { get; init; }
    public required DateTime? SentAt { get; init; }
    public required DateTime? ReceivedAt { get; init; }
    public required EmailImportance Importance { get; init; }
    public required bool? IsRead { get; init; }
    public required bool HasAttachments { get; init; }
    public required EmailMessageBodyType BodyType { get; init; }

    /// <summary>Safe plain-text body. Truncated at server-side limit.</summary>
    public required string? BodyText { get; init; }

    /// <summary>HTML body. Client MUST sanitize before rendering. Never set InnerHtml directly.</summary>
    public required string? BodyHtml { get; init; }
    public required string? BodyPreview { get; init; }

    public required string? InternetMessageId { get; init; }
    public required string? ThreadId { get; init; }
    public required MessageImportStatus ImportStatus { get; init; }
    public required DateTime? ImportedAt { get; init; }
    public required IReadOnlyList<AttachmentReferenceDto> Attachments { get; init; }
}

public sealed record RecipientDto(
    string RecipientType,
    string EmailAddress,
    string? DisplayName);

public sealed record AttachmentReferenceDto
{
    public required Guid Id { get; init; }
    public required string FileName { get; init; }
    public required string? MimeType { get; init; }
    public required long? SizeBytes { get; init; }
    public required bool IsInline { get; init; }
    public required string DispatchStatus { get; init; }
    public required Guid? DocumentReferenceId { get; init; }
}
