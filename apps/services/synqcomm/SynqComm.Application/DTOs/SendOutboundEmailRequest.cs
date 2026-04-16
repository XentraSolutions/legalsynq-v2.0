namespace SynqComm.Application.DTOs;

public record SendOutboundEmailRequest(
    Guid ConversationId,
    Guid MessageId,
    string ToAddresses,
    string? CcAddresses = null,
    string? BccAddresses = null,
    string? SubjectOverride = null,
    string? BodyTextOverride = null,
    string? BodyHtmlOverride = null,
    Guid? ReplyToEmailReferenceId = null,
    List<Guid>? AttachmentDocumentIds = null);
