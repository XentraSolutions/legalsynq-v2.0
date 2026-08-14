namespace Intake.Domain.Emails;

public sealed class InboundEmail
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid? OrgId { get; set; }
    public Guid TenantIntakeSourceId { get; set; }
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
    public string ReferencesJson { get; set; } = "[]";
    public DateTimeOffset ReceivedAt { get; set; }
    public DateTimeOffset? ProviderCreatedAt { get; set; }
    public DateTimeOffset CapturedAt { get; set; }
    public string? FromAddress { get; set; }
    public string? FromDisplayName { get; set; }
    public string? SenderAddress { get; set; }
    public string? SenderDisplayName { get; set; }
    public string? ReplyToAddress { get; set; }
    public string? ReplyToDisplayName { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string? TextBody { get; set; }
    public string? HtmlBody { get; set; }
    public string HeadersJson { get; set; } = "[]";
    public string? RawMessageContent { get; set; }
    public string? RawMessageHash { get; set; }
    public long? RawMessageSizeBytes { get; set; }
    public bool HasAttachments { get; set; }
    public int AttachmentCount { get; set; }
    public string CaptureStatus { get; set; } = string.Empty;
    public string ProcessingStatus { get; set; } = string.Empty;
    public string IdempotencyKey { get; set; } = string.Empty;
    public int DuplicateCaptureCount { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public ICollection<InboundEmailRecipient> Recipients { get; set; } = [];
    public ICollection<InboundEmailAttachmentMetadata> AttachmentMetadata { get; set; } = [];
}