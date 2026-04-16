using Microsoft.Extensions.Logging;
using SynqComm.Application.DTOs;
using SynqComm.Application.Interfaces;
using SynqComm.Application.Repositories;
using SynqComm.Domain.Entities;
using SynqComm.Domain.Enums;

namespace SynqComm.Application.Services;

public class ConversationService : IConversationService
{
    private readonly IConversationRepository _repo;
    private readonly IAuditPublisher _audit;
    private readonly ILogger<ConversationService> _logger;

    public ConversationService(
        IConversationRepository repo,
        IAuditPublisher audit,
        ILogger<ConversationService> logger)
    {
        _repo = repo;
        _audit = audit;
        _logger = logger;
    }

    public async Task<ConversationResponse> CreateAsync(
        Guid tenantId, Guid orgId, Guid userId,
        CreateConversationRequest request, CancellationToken ct = default)
    {
        var conversation = Conversation.Create(
            tenantId, orgId,
            request.ProductKey, request.ContextType, request.ContextId,
            request.Subject, request.VisibilityType,
            userId);

        await _repo.AddAsync(conversation, ct);

        _logger.LogInformation("Conversation {ConversationId} created for context {ContextType}/{ContextId}",
            conversation.Id, conversation.ContextType, conversation.ContextId);

        _audit.Publish("ConversationCreated", "Created", $"Conversation created: {request.Subject}",
            tenantId, userId, "Conversation", conversation.Id.ToString());

        return ToResponse(conversation);
    }

    public async Task<ConversationResponse?> GetByIdAsync(
        Guid tenantId, Guid id, CancellationToken ct = default)
    {
        var conversation = await _repo.GetByIdAsync(tenantId, id, ct);
        return conversation is null ? null : ToResponse(conversation);
    }

    public async Task<List<ConversationResponse>> ListByContextAsync(
        Guid tenantId, string contextType, string contextId, CancellationToken ct = default)
    {
        if (!ContextType.All.Contains(contextType))
            throw new ArgumentException($"Invalid context type: '{contextType}'.");

        var conversations = await _repo.ListByContextAsync(tenantId, contextType, contextId, ct);
        return conversations.Select(ToResponse).ToList();
    }

    public async Task<ConversationResponse> UpdateStatusAsync(
        Guid tenantId, Guid id, Guid userId,
        UpdateConversationStatusRequest request, CancellationToken ct = default)
    {
        var conversation = await _repo.GetByIdAsync(tenantId, id, ct)
            ?? throw new KeyNotFoundException($"Conversation '{id}' not found.");

        conversation.UpdateStatus(request.Status, userId);
        await _repo.UpdateAsync(conversation, ct);

        _logger.LogInformation("Conversation {ConversationId} status changed to {Status}",
            conversation.Id, request.Status);

        _audit.Publish("ConversationStatusChanged", "StatusChanged", $"Status changed to {request.Status}",
            tenantId, userId, "Conversation", conversation.Id.ToString());

        return ToResponse(conversation);
    }

    private static ConversationResponse ToResponse(Conversation c) => new(
        c.Id, c.TenantId, c.OrgId,
        c.ProductKey, c.ContextType, c.ContextId,
        c.Subject, c.Status, c.VisibilityType,
        c.LastActivityAtUtc, c.CreatedAtUtc, c.UpdatedAtUtc, c.CreatedByUserId);
}
