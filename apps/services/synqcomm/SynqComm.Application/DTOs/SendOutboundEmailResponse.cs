namespace SynqComm.Application.DTOs;

public record SendOutboundEmailResponse(
    Guid ConversationId,
    Guid MessageId,
    Guid EmailMessageReferenceId,
    string DeliveryStatus,
    Guid? NotificationsRequestId,
    string GeneratedInternetMessageId,
    Guid? MatchedReplyReferenceId,
    int AttachmentCount);
