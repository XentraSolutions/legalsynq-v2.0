using Microsoft.Extensions.Logging;
using SynqComm.Application.DTOs;
using SynqComm.Application.Interfaces;
using SynqComm.Application.Repositories;
using SynqComm.Domain.Entities;
using SynqComm.Domain.Enums;

namespace SynqComm.Application.Services;

public class MessageService : IMessageService
{
    private readonly IMessageRepository _messageRepo;
    private readonly IConversationRepository _conversationRepo;
    private readonly IAuditPublisher _audit;
    private readonly ILogger<MessageService> _logger;

    public MessageService(
        IMessageRepository messageRepo,
        IConversationRepository conversationRepo,
        IAuditPublisher audit,
        ILogger<MessageService> logger)
    {
        _messageRepo = messageRepo;
        _conversationRepo = conversationRepo;
        _audit = audit;
        _logger = logger;
    }

    public async Task<MessageResponse> AddAsync(
        Guid tenantId, Guid orgId, Guid userId, Guid conversationId,
        AddMessageRequest request, CancellationToken ct = default)
    {
        var conversation = await _conversationRepo.GetByIdAsync(tenantId, conversationId, ct)
            ?? throw new KeyNotFoundException($"Conversation '{conversationId}' not found.");

        var message = Message.Create(
            conversationId, tenantId, orgId,
            request.Channel, request.Direction,
            request.Body, request.VisibilityType,
            userId,
            senderUserId: userId,
            senderParticipantType: ParticipantType.InternalUser);

        await _messageRepo.AddAsync(message, ct);

        conversation.TouchActivity();
        await _conversationRepo.UpdateAsync(conversation, ct);

        _logger.LogInformation("Message {MessageId} added to conversation {ConversationId}",
            message.Id, conversationId);

        _audit.Publish("MessageAdded", "Created", $"Message added to conversation {conversationId}",
            tenantId, userId, "Message", message.Id.ToString());

        return ToResponse(message);
    }

    public async Task<List<MessageResponse>> ListByConversationAsync(
        Guid tenantId, Guid conversationId, CancellationToken ct = default)
    {
        var messages = await _messageRepo.ListByConversationAsync(tenantId, conversationId, ct);
        return messages.Select(ToResponse).ToList();
    }

    private static MessageResponse ToResponse(Message m) => new(
        m.Id, m.ConversationId,
        m.Channel, m.Direction, m.Body, m.VisibilityType,
        m.SentAtUtc, m.SenderUserId, m.SenderParticipantType,
        m.ExternalSenderName, m.ExternalSenderEmail,
        m.Status, m.CreatedAtUtc);
}
