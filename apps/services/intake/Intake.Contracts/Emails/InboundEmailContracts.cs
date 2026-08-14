namespace Intake.Contracts.Emails;

/// <summary>
/// Trusted application-layer capture input. Tenant and source values must come
/// from B03 source resolution or another trusted connector boundary, not from a
/// normal tenant-facing request body.
/// </summary>
public sealed class CaptureInboundEmailCommand
{
    public Guid TenantId { get; set; }
    public Guid SourceId { get; set; }
    public int SourceConfigurationVersion { get; set; }
    public string Purpose { get; set; } = string.Empty;
    public string ProcessingProfileCode { get; set; } = string.Empty;
    public int? TenantConfigurationVersion { get; set; }
    public int? TenantProfileConfigurationVersion { get; set; }
    public string Provider { get; set; } = string.Empty;
    public string? ProviderMessageId { get; set; }
    public string? ProviderThreadId { get; set; }
    public string? InternetMessageId { get; set; }
    public string? InReplyToMessageId { get; set; }
    public List<string> References { get; set; } = [];
    public DateTimeOffset ReceivedAt { get; set; }
    public DateTimeOffset? ProviderCreatedAt { get; set; }
    public string? FromAddress { get; set; }
    public string? FromDisplayName { get; set; }
    public string? SenderAddress { get; set; }
    public string? SenderDisplayName { get; set; }
    public string? ReplyToAddress { get; set; }
    public string? ReplyToDisplayName { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string? TextBody { get; set; }
    public string? HtmlBody { get; set; }
    public List<InboundEmailRecipientInput> Recipients { get; set; } = [];
    public List<InboundEmailHeaderInput> Headers { get; set; } = [];
    public List<InboundEmailAttachmentInput> Attachments { get; set; } = [];
    public string? RawMessage { get; set; }
}

public sealed class InboundEmailRecipientInput
{
    public string RecipientType { get; set; } = string.Empty;
    public string EmailAddress { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
}

public sealed class InboundEmailHeaderInput
{
    public string Name { get; set; } = string.Empty;
    public List<string> Values { get; set; } = [];
}

public sealed class InboundEmailAttachmentInput
{
    public string? ProviderAttachmentId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string? ContentType { get; set; }
    public string? ContentDisposition { get; set; }
    public string? ContentId { get; set; }
    public long? SizeBytes { get; set; }
    public string? Sha256 { get; set; }
    public bool IsInline { get; set; }
}

public sealed record InboundEmailCaptureResponse(
    InboundEmailDetailResponse Email,
    bool IsDuplicate);

public sealed class InboundEmailListQuery
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
    public Guid? SourceId { get; set; }
    public string? Provider { get; set; }
    public string? Purpose { get; set; }
    public string? ProcessingProfileCode { get; set; }
    public string? CaptureStatus { get; set; }
    public DateTimeOffset? FromDate { get; set; }
    public DateTimeOffset? ToDate { get; set; }
    public bool? HasAttachments { get; set; }
    public string? FromAddress { get; set; }
}

public sealed record InboundEmailListItemResponse(
    Guid Id,
    Guid TenantIntakeSourceId,
    string Purpose,
    string ProcessingProfileCode,
    string Provider,
    string? ProviderMessageId,
    string? InternetMessageId,
    DateTimeOffset ReceivedAt,
    string? FromAddress,
    string? FromDisplayName,
    string Subject,
    bool HasAttachments,
    int AttachmentCount,
    string CaptureStatus,
    string ProcessingStatus,
    int DuplicateCaptureCount);

public sealed record PagedInboundEmailResponse(
    IReadOnlyList<InboundEmailListItemResponse> Items,
    int Page,
    int PageSize,
    long TotalCount,
    int TotalPages);

public sealed record InboundEmailRecipientResponse(
    Guid Id,
    string RecipientType,
    string EmailAddress,
    string NormalizedEmailAddress,
    string? DisplayName,
    int Ordinal);

public sealed record InboundEmailAttachmentResponse(
    Guid Id,
    string? ProviderAttachmentId,
    string FileName,
    string? ContentType,
    string? ContentDisposition,
    string? ContentId,
    long? SizeBytes,
    string? Sha256,
    bool IsInline,
    int Ordinal);

public sealed record InboundEmailDetailResponse(
    Guid Id,
    Guid TenantId,
    Guid? OrgId,
    Guid TenantIntakeSourceId,
    int SourceConfigurationVersion,
    string Purpose,
    string ProcessingProfileCode,
    int? TenantConfigurationVersion,
    int? TenantProfileConfigurationVersion,
    string Provider,
    string? ProviderMessageId,
    string? ProviderThreadId,
    string? InternetMessageId,
    string? InReplyToMessageId,
    IReadOnlyList<string> References,
    DateTimeOffset ReceivedAt,
    DateTimeOffset? ProviderCreatedAt,
    DateTimeOffset CapturedAt,
    string? FromAddress,
    string? FromDisplayName,
    string? SenderAddress,
    string? SenderDisplayName,
    string? ReplyToAddress,
    string? ReplyToDisplayName,
    string Subject,
    string? TextBody,
    /// <param name="HtmlBody">
    /// Original HTML source. This value is untrusted and must be sanitized or
    /// isolated before any downstream browser rendering.
    /// </param>
    string? HtmlBody,
    string HeadersJson,
    long? RawMessageSizeBytes,
    string? RawMessageHash,
    bool HasRawMessage,
    bool HasAttachments,
    int AttachmentCount,
    string CaptureStatus,
    string ProcessingStatus,
    int DuplicateCaptureCount,
    IReadOnlyList<InboundEmailRecipientResponse> Recipients,
    IReadOnlyList<InboundEmailAttachmentResponse> Attachments,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record InboundEmailAnalyticsResponse(
    long TotalEmails,
    IReadOnlyList<InboundEmailCountByKey> EmailsByDay,
    IReadOnlyList<InboundEmailCountByKey> EmailsBySource,
    IReadOnlyList<InboundEmailCountByKey> EmailsByProvider,
    IReadOnlyList<InboundEmailCountByKey> EmailsByPurpose,
    IReadOnlyList<InboundEmailCountByKey> EmailsByCaptureStatus,
    long EmailsWithAttachments,
    decimal AverageAttachmentsPerEmail,
    long DuplicateDeliveriesPrevented,
    long CaptureFailures);

public sealed record InboundEmailCountByKey(string Key, long Count);