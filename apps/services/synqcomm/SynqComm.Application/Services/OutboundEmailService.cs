using Microsoft.Extensions.Logging;
using SynqComm.Application.DTOs;
using SynqComm.Application.Interfaces;
using SynqComm.Application.Repositories;
using SynqComm.Domain.Entities;
using SynqComm.Domain.Enums;

namespace SynqComm.Application.Services;

public class OutboundEmailService : IOutboundEmailService
{
    private readonly IConversationRepository _conversationRepo;
    private readonly IMessageRepository _messageRepo;
    private readonly IParticipantRepository _participantRepo;
    private readonly IMessageAttachmentRepository _attachmentRepo;
    private readonly IEmailMessageReferenceRepository _emailRefRepo;
    private readonly IEmailDeliveryStateRepository _deliveryRepo;
    private readonly INotificationsServiceClient _notificationsClient;
    private readonly IAuditPublisher _audit;
    private readonly ILogger<OutboundEmailService> _logger;

    private static readonly Guid SystemUserId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    public OutboundEmailService(
        IConversationRepository conversationRepo,
        IMessageRepository messageRepo,
        IParticipantRepository participantRepo,
        IMessageAttachmentRepository attachmentRepo,
        IEmailMessageReferenceRepository emailRefRepo,
        IEmailDeliveryStateRepository deliveryRepo,
        INotificationsServiceClient notificationsClient,
        IAuditPublisher audit,
        ILogger<OutboundEmailService> logger)
    {
        _conversationRepo = conversationRepo;
        _messageRepo = messageRepo;
        _participantRepo = participantRepo;
        _attachmentRepo = attachmentRepo;
        _emailRefRepo = emailRefRepo;
        _deliveryRepo = deliveryRepo;
        _notificationsClient = notificationsClient;
        _audit = audit;
        _logger = logger;
    }

    public async Task<SendOutboundEmailResponse> SendOutboundAsync(
        SendOutboundEmailRequest request, Guid tenantId, Guid orgId, Guid userId,
        CancellationToken ct = default)
    {
        var conversation = await _conversationRepo.GetByIdAsync(tenantId, request.ConversationId, ct)
            ?? throw new KeyNotFoundException($"Conversation '{request.ConversationId}' not found.");

        var participant = await _participantRepo.GetActiveByUserIdAsync(tenantId, request.ConversationId, userId, ct)
            ?? throw new UnauthorizedAccessException("You are not an active participant in this conversation.");

        if (participant.ParticipantType != ParticipantType.InternalUser)
            throw new UnauthorizedAccessException("Only internal users can send outbound email.");

        if (!participant.CanReply)
            throw new UnauthorizedAccessException("You do not have reply permissions in this conversation.");

        var message = await _messageRepo.GetByIdAsync(tenantId, request.ConversationId, request.MessageId, ct)
            ?? throw new KeyNotFoundException($"Message '{request.MessageId}' not found.");

        if (message.VisibilityType != VisibilityType.SharedExternal)
        {
            _audit.Publish("OutboundEmailRejected", "Rejected",
                $"Outbound email rejected: message visibility is {message.VisibilityType}, not SharedExternal",
                tenantId, userId, "Message", message.Id.ToString(),
                metadata: $"{{\"reason\":\"visibility_mismatch\",\"visibilityType\":\"{message.VisibilityType}\"}}");

            throw new InvalidOperationException(
                $"Only messages with SharedExternal visibility can be sent as outbound email. Current: {message.VisibilityType}");
        }

        if (message.Channel == Channel.SystemNote)
        {
            _audit.Publish("OutboundEmailRejected", "Rejected",
                "Outbound email rejected: SystemNote messages cannot be sent externally",
                tenantId, userId, "Message", message.Id.ToString());

            throw new InvalidOperationException("SystemNote messages cannot be sent as outbound email.");
        }

        var existingOutbound = await _emailRefRepo.FindByMessageIdAsync(tenantId, message.Id, ct);
        if (existingOutbound is not null && existingOutbound.EmailDirection == EmailDirection.Outbound)
            throw new InvalidOperationException("An outbound email has already been sent for this message.");

        var internetMessageId = EmailMessageReference.GenerateInternetMessageId(conversation.Id);
        var subject = request.SubjectOverride ?? conversation.Subject;
        var bodyText = request.BodyTextOverride ?? message.BodyPlainText ?? message.Body;
        var bodyHtml = request.BodyHtmlOverride ?? message.Body;

        string? inReplyToMessageId = null;
        string? referencesHeader = null;
        Guid? matchedReplyReferenceId = null;

        if (request.ReplyToEmailReferenceId.HasValue)
        {
            var replyRef = await _emailRefRepo.GetByIdAsync(tenantId, request.ReplyToEmailReferenceId.Value, ct);
            if (replyRef is not null && replyRef.ConversationId == conversation.Id)
            {
                inReplyToMessageId = replyRef.InternetMessageId;
                matchedReplyReferenceId = replyRef.Id;

                var chainRefs = new List<string>();
                if (!string.IsNullOrWhiteSpace(replyRef.ReferencesHeader))
                    chainRefs.AddRange(replyRef.ReferencesHeader.Split(' ', StringSplitOptions.RemoveEmptyEntries));
                if (!string.IsNullOrWhiteSpace(replyRef.InternetMessageId))
                    chainRefs.Add(replyRef.InternetMessageId);
                referencesHeader = string.Join(" ", chainRefs.Distinct());
            }
        }
        else
        {
            var latestRef = await _emailRefRepo.GetLatestByConversationAsync(tenantId, conversation.Id, ct);
            if (latestRef is not null)
            {
                inReplyToMessageId = latestRef.InternetMessageId;
                matchedReplyReferenceId = latestRef.Id;

                var chainRefs = new List<string>();
                if (!string.IsNullOrWhiteSpace(latestRef.ReferencesHeader))
                    chainRefs.AddRange(latestRef.ReferencesHeader.Split(' ', StringSplitOptions.RemoveEmptyEntries));
                if (!string.IsNullOrWhiteSpace(latestRef.InternetMessageId))
                    chainRefs.Add(latestRef.InternetMessageId);
                referencesHeader = string.Join(" ", chainRefs.Distinct());
            }
        }

        var attachments = new List<OutboundAttachmentDescriptor>();
        if (request.AttachmentDocumentIds is { Count: > 0 })
        {
            var messageAttachments = await _attachmentRepo.ListByMessageAsync(tenantId, message.Id, ct);
            foreach (var docId in request.AttachmentDocumentIds)
            {
                var att = messageAttachments.FirstOrDefault(a => a.DocumentId == docId && a.IsActive);
                if (att is not null)
                {
                    attachments.Add(new OutboundAttachmentDescriptor(
                        att.DocumentId, att.FileName, att.ContentType, att.FileSizeBytes));
                }
            }
        }

        var fromEmail = "noreply@legalsynq.com";
        var fromDisplayName = "LegalSynq Communications";

        var sendAttemptId = Guid.NewGuid();
        var idempotencyKey = $"synqcomm-outbound-{sendAttemptId}";
        var payload = new OutboundEmailPayload(
            TenantId: tenantId,
            FromEmail: fromEmail,
            FromDisplayName: fromDisplayName,
            ToAddresses: request.ToAddresses,
            CcAddresses: request.CcAddresses,
            Subject: subject,
            BodyText: bodyText,
            BodyHtml: bodyHtml,
            InternetMessageId: internetMessageId,
            InReplyToMessageId: inReplyToMessageId,
            ReferencesHeader: referencesHeader,
            IdempotencyKey: idempotencyKey,
            Attachments: attachments.Count > 0 ? attachments : null);

        var sendResult = await _notificationsClient.SendEmailAsync(payload, ct);

        if (!sendResult.Success)
        {
            _audit.Publish("OutboundEmailFailed", "Failed",
                $"Outbound email failed: {subject}",
                tenantId, userId, "Message", message.Id.ToString(),
                metadata: $"{{\"internetMessageId\":\"{internetMessageId}\",\"conversationId\":\"{conversation.Id}\",\"messageId\":\"{message.Id}\",\"toAddresses\":\"{request.ToAddresses}\",\"errorMessage\":\"{sendResult.ErrorMessage}\"}}");

            _logger.LogWarning(
                "Outbound email failed: ConversationId={ConversationId} MessageId={MessageId} Error={Error}",
                conversation.Id, message.Id, sendResult.ErrorMessage);

            throw new InvalidOperationException(
                $"Failed to submit outbound email to Notifications service: {sendResult.ErrorMessage}");
        }

        var emailRef = EmailMessageReference.Create(
            tenantId, conversation.Id, message.Id,
            internetMessageId, EmailDirection.Outbound,
            fromEmail, request.ToAddresses, subject,
            userId,
            inReplyToMessageId: inReplyToMessageId,
            referencesHeader: referencesHeader,
            ccAddresses: request.CcAddresses,
            fromDisplayName: fromDisplayName);

        await _emailRefRepo.AddAsync(emailRef, ct);

        var deliveryState = EmailDeliveryState.Create(
            tenantId, conversation.Id, message.Id, emailRef.Id,
            sendResult.NotificationsRequestId?.ToString(),
            sendResult.ProviderUsed,
            sendResult.ProviderMessageId,
            DeliveryStatus.Queued,
            userId);

        await _deliveryRepo.AddAsync(deliveryState, ct);

        conversation.TouchActivity();
        conversation.AutoTransitionToOpen(userId);
        if (conversation.Status == ConversationStatus.Closed)
        {
            conversation.ReopenFromClosed(userId);
            _audit.Publish("ConversationReopened", "Updated",
                "Conversation reopened due to outbound email",
                tenantId, userId, "Conversation", conversation.Id.ToString());
        }
        await _conversationRepo.UpdateAsync(conversation, ct);

        _audit.Publish("OutboundEmailQueued", "Created",
            $"Outbound email queued: {subject}",
            tenantId, userId, "EmailMessageReference", emailRef.Id.ToString(),
            metadata: $"{{\"internetMessageId\":\"{internetMessageId}\",\"conversationId\":\"{conversation.Id}\",\"messageId\":\"{message.Id}\",\"toAddresses\":\"{request.ToAddresses}\",\"attachmentCount\":{attachments.Count},\"deliveryStatus\":\"{DeliveryStatus.Queued}\",\"notificationsRequestId\":\"{sendResult.NotificationsRequestId}\"}}");

        _logger.LogInformation(
            "Outbound email queued: ConversationId={ConversationId} MessageId={MessageId} InternetMessageId={InternetMessageId}",
            conversation.Id, message.Id, internetMessageId);

        return new SendOutboundEmailResponse(
            conversation.Id, message.Id, emailRef.Id,
            DeliveryStatus.Queued,
            sendResult.NotificationsRequestId,
            internetMessageId,
            matchedReplyReferenceId,
            attachments.Count);
    }

    public async Task<bool> ProcessDeliveryStatusAsync(
        DeliveryStatusUpdateRequest request, Guid tenantId,
        CancellationToken ct = default)
    {
        EmailDeliveryState? deliveryState = null;

        if (!string.IsNullOrWhiteSpace(request.ProviderMessageId))
            deliveryState = await _deliveryRepo.FindByProviderMessageIdAsync(tenantId, request.ProviderMessageId, ct);

        if (deliveryState is null && !string.IsNullOrWhiteSpace(request.InternetMessageId))
        {
            var emailRef = await _emailRefRepo.FindByInternetMessageIdAsync(tenantId, request.InternetMessageId, ct);
            if (emailRef is not null)
                deliveryState = await _deliveryRepo.FindByEmailReferenceIdAsync(tenantId, emailRef.Id, ct);
        }

        if (deliveryState is null)
        {
            _logger.LogWarning(
                "Delivery status update could not be matched: ProviderMessageId={ProviderMessageId} InternetMessageId={InternetMessageId}",
                request.ProviderMessageId, request.InternetMessageId);
            return false;
        }

        var normalizedStatus = NormalizeDeliveryStatus(request.Status);
        var updated = deliveryState.UpdateStatus(
            normalizedStatus, request.StatusAtUtc,
            request.ErrorCode, request.ErrorMessage,
            request.RetryCount, request.ProviderMessageId,
            SystemUserId);

        if (!updated)
        {
            _logger.LogInformation(
                "Delivery status update ignored (already terminal): DeliveryStateId={Id} Status={Status}",
                deliveryState.Id, deliveryState.DeliveryStatus);
            return true;
        }

        await _deliveryRepo.UpdateAsync(deliveryState, ct);

        if (normalizedStatus == DeliveryStatus.Sent || normalizedStatus == DeliveryStatus.Delivered)
        {
            var emailRef = await _emailRefRepo.GetByIdAsync(tenantId, deliveryState.EmailMessageReferenceId, ct);
            if (emailRef is not null && emailRef.SentAtUtc is null)
            {
                emailRef.SetSentAtUtc(request.StatusAtUtc, SystemUserId);
                await _emailRefRepo.UpdateAsync(emailRef, ct);
            }
        }

        _audit.Publish("OutboundEmailDeliveryUpdate", "Updated",
            $"Delivery status updated to {normalizedStatus}",
            tenantId, null, "EmailDeliveryState", deliveryState.Id.ToString(),
            metadata: $"{{\"deliveryStatus\":\"{normalizedStatus}\",\"providerMessageId\":\"{request.ProviderMessageId}\",\"provider\":\"{request.Provider}\",\"emailMessageReferenceId\":\"{deliveryState.EmailMessageReferenceId}\"}}");

        _logger.LogInformation(
            "Delivery status updated: DeliveryStateId={Id} Status={Status} Provider={Provider}",
            deliveryState.Id, normalizedStatus, request.Provider);

        return true;
    }

    public async Task<List<EmailDeliveryStateResponse>> ListDeliveryStatesAsync(
        Guid tenantId, Guid conversationId, Guid userId,
        CancellationToken ct = default)
    {
        var participant = await _participantRepo.GetActiveByUserIdAsync(tenantId, conversationId, userId, ct)
            ?? throw new UnauthorizedAccessException("You are not an active participant in this conversation.");

        var states = await _deliveryRepo.ListByConversationAsync(tenantId, conversationId, ct);
        return states.Select(ToResponse).ToList();
    }

    private static string NormalizeDeliveryStatus(string status)
    {
        return status?.Trim().ToLowerInvariant() switch
        {
            "pending" => DeliveryStatus.Pending,
            "queued" => DeliveryStatus.Queued,
            "sent" => DeliveryStatus.Sent,
            "delivered" => DeliveryStatus.Delivered,
            "failed" => DeliveryStatus.Failed,
            "bounced" or "bounce" => DeliveryStatus.Bounced,
            "deferred" => DeliveryStatus.Deferred,
            "suppressed" => DeliveryStatus.Suppressed,
            _ => DeliveryStatus.Unknown,
        };
    }

    private static EmailDeliveryStateResponse ToResponse(EmailDeliveryState e) => new(
        e.Id, e.ConversationId, e.MessageId, e.EmailMessageReferenceId,
        e.DeliveryStatus, e.ProviderName, e.ProviderMessageId,
        e.NotificationsRequestId, e.LastStatusAtUtc,
        e.LastErrorCode, e.LastErrorMessage, e.RetryCount, e.CreatedAtUtc);
}
