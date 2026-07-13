using Xenia.Domain.Common;

namespace Xenia.Domain.Assistant;

public sealed class AssistantMessage : AuditableEntityBase
{
    public const int RoleMaxLength = 30;
    public const int ProviderMaxLength = 50;
    public const int ProviderResponseIdMaxLength = 200;
    public const int FinishReasonMaxLength = 100;

    private AssistantMessage() { }

    public AssistantMessage(
        Guid id,
        Guid conversationId,
        Guid tenantId,
        Guid actorId,
        AssistantMessageRole role,
        string content,
        string provider,
        string? providerResponseId,
        string? metadataJson)
    {
        Id = id == Guid.Empty ? throw new ArgumentException("Message id must not be empty.", nameof(id)) : id;
        ConversationId = conversationId == Guid.Empty ? throw new ArgumentException("Conversation id must not be empty.", nameof(conversationId)) : conversationId;
        TenantId = tenantId == Guid.Empty ? throw new ArgumentException("Tenant id must not be empty.", nameof(tenantId)) : tenantId;
        ActorId = actorId == Guid.Empty ? throw new ArgumentException("Actor id must not be empty.", nameof(actorId)) : actorId;
        Role = role;
        Content = content ?? string.Empty;
        Provider = string.IsNullOrWhiteSpace(provider) ? "internal" : provider.Trim();
        ProviderResponseId = string.IsNullOrWhiteSpace(providerResponseId) ? null : providerResponseId.Trim();
        MetadataJson = string.IsNullOrWhiteSpace(metadataJson) ? "{}" : metadataJson;
        CreatedAtMessageUtc = DateTime.UtcNow;
    }

    public Guid Id { get; private set; }
    public Guid ConversationId { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid ActorId { get; private set; }
    public AssistantMessageRole Role { get; private set; }
    public string Content { get; private set; } = string.Empty;
    public string Provider { get; private set; } = "internal";
    public string? ProviderResponseId { get; private set; }
    public int? InputTokens { get; private set; }
    public int? OutputTokens { get; private set; }
    public string? FinishReason { get; private set; }
    public string MetadataJson { get; private set; } = "{}";
    public DateTime CreatedAtMessageUtc { get; private set; }
    public uint RowVersion { get; private set; }

    public void SetUsage(int? inputTokens, int? outputTokens, string? finishReason)
    {
        InputTokens = inputTokens;
        OutputTokens = outputTokens;
        FinishReason = string.IsNullOrWhiteSpace(finishReason) ? null : finishReason.Trim();
    }
}
