using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SynqComm.Application.Interfaces;
using SynqComm.Application.Repositories;
using SynqComm.Infrastructure.Persistence;
using SynqComm.Infrastructure.Repositories;
using SynqComm.Application.Services;
using SynqComm.Domain.Entities;
using SynqComm.Domain.Enums;

namespace SynqComm.Tests;

public static class TestHelpers
{
    public static readonly Guid TenantId = Guid.NewGuid();
    public static readonly Guid OrgId = Guid.NewGuid();
    public static readonly Guid UserId1 = Guid.NewGuid();
    public static readonly Guid UserId2 = Guid.NewGuid();
    public static readonly Guid ExternalUserId = Guid.NewGuid();

    public static SynqCommDbContext CreateDbContext(string? dbName = null)
    {
        var options = new DbContextOptionsBuilder<SynqCommDbContext>()
            .UseInMemoryDatabase(dbName ?? Guid.NewGuid().ToString())
            .Options;

        return new SynqCommDbContext(options);
    }

    public static Conversation CreateTestConversation(
        string status = "New",
        string visibility = "InternalOnly")
    {
        return Conversation.Create(
            TenantId, OrgId, "SYNQ_COMMS",
            ContextType.Case, "case-123",
            "Test Conversation", visibility, UserId1);
    }

    public static ConversationParticipant CreateTestParticipant(
        Guid conversationId,
        Guid? userId = null,
        string participantType = "InternalUser",
        bool canReply = true,
        string? externalName = null,
        string? externalEmail = null)
    {
        return ConversationParticipant.Create(
            conversationId, TenantId, OrgId,
            participantType, ParticipantRole.Participant, canReply,
            UserId1,
            userId: userId ?? UserId1,
            externalName: externalName,
            externalEmail: externalEmail);
    }

    public static Message CreateTestMessage(
        Guid conversationId,
        string visibility = "InternalOnly",
        string channel = "InApp",
        Guid? senderUserId = null)
    {
        return Message.Create(
            conversationId, TenantId, OrgId,
            channel, Direction.Internal,
            $"Test message {Guid.NewGuid()}", visibility,
            senderUserId ?? UserId1,
            senderUserId: senderUserId ?? UserId1,
            senderParticipantType: ParticipantType.InternalUser);
    }

    public static IConversationRepository CreateConversationRepo(SynqCommDbContext db) =>
        new ConversationRepository(db);

    public static IMessageRepository CreateMessageRepo(SynqCommDbContext db) =>
        new MessageRepository(db);

    public static IParticipantRepository CreateParticipantRepo(SynqCommDbContext db) =>
        new ParticipantRepository(db);

    public static IConversationReadStateRepository CreateReadStateRepo(SynqCommDbContext db) =>
        new ConversationReadStateRepository(db);

    public static IMessageAttachmentRepository CreateAttachmentRepo(SynqCommDbContext db) =>
        new MessageAttachmentRepository(db);

    public static IEmailMessageReferenceRepository CreateEmailRefRepo(SynqCommDbContext db) =>
        new EmailMessageReferenceRepository(db);

    public static IExternalParticipantIdentityRepository CreateIdentityRepo(SynqCommDbContext db) =>
        new ExternalParticipantIdentityRepository(db);

    public static IEmailDeliveryStateRepository CreateDeliveryStateRepo(SynqCommDbContext db) =>
        new EmailDeliveryStateRepository(db);

    public static IEmailRecipientRecordRepository CreateRecipientRepo(SynqCommDbContext db) =>
        new EmailRecipientRecordRepository(db);

    public static ITenantEmailSenderConfigRepository CreateSenderConfigRepo(SynqCommDbContext db) =>
        new TenantEmailSenderConfigRepository(db);

    public static IEmailTemplateConfigRepository CreateTemplateConfigRepo(SynqCommDbContext db) =>
        new EmailTemplateConfigRepository(db);

    public static ILogger<T> CreateLogger<T>() =>
        LoggerFactory.Create(b => { }).CreateLogger<T>();
}

public class NoOpAuditPublisher : IAuditPublisher
{
    public List<(string EventType, string Action, string Description)> Events { get; } = new();

    public void Publish(
        string eventType, string action, string description,
        Guid tenantId, Guid? actorUserId = null,
        string? entityType = null, string? entityId = null,
        string? before = null, string? after = null,
        string? metadata = null)
    {
        Events.Add((eventType, action, description));
    }
}

public class MockNotificationsServiceClient : INotificationsServiceClient
{
    public List<OutboundEmailPayload> SentPayloads { get; } = new();
    public NotificationsSendResult NextResult { get; set; } = new(
        Success: true,
        NotificationsRequestId: Guid.NewGuid(),
        ProviderUsed: "test-provider",
        ProviderMessageId: null,
        Status: "queued",
        ErrorMessage: null);

    public Task<NotificationsSendResult> SendEmailAsync(OutboundEmailPayload payload, CancellationToken ct = default)
    {
        SentPayloads.Add(payload);
        return Task.FromResult(NextResult);
    }
}

public class MockDocumentServiceClient : IDocumentServiceClient
{
    private readonly Dictionary<Guid, DocumentValidationResult> _results = new();

    public void SetResult(Guid documentId, DocumentValidationResult result) =>
        _results[documentId] = result;

    public Task<DocumentValidationResult> ValidateDocumentAsync(
        Guid documentId, Guid expectedTenantId, CancellationToken ct = default)
    {
        if (_results.TryGetValue(documentId, out var result))
            return Task.FromResult(result);

        return Task.FromResult(new DocumentValidationResult(false, null));
    }
}
