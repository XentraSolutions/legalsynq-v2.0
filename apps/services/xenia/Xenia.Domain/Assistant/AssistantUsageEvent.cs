using Xenia.Domain.Common;

namespace Xenia.Domain.Assistant;

public sealed class AssistantUsageEvent : AuditableEntityBase
{
    public const int ProviderMaxLength = 50;
    public const int ModelKeyMaxLength = 100;
    public const int EventTypeMaxLength = 50;

    private AssistantUsageEvent() { }

    public AssistantUsageEvent(
        Guid id,
        Guid tenantId,
        Guid actorId,
        Guid conversationId,
        Guid? messageId,
        string agentKey,
        string provider,
        string modelKey,
        string eventType,
        int inputTokens,
        int outputTokens,
        decimal estimatedCostUsd,
        int latencyMs)
    {
        Id = id == Guid.Empty ? throw new ArgumentException("Usage event id must not be empty.", nameof(id)) : id;
        TenantId = tenantId == Guid.Empty ? throw new ArgumentException("Tenant id must not be empty.", nameof(tenantId)) : tenantId;
        ActorId = actorId == Guid.Empty ? throw new ArgumentException("Actor id must not be empty.", nameof(actorId)) : actorId;
        ConversationId = conversationId == Guid.Empty ? throw new ArgumentException("Conversation id must not be empty.", nameof(conversationId)) : conversationId;
        MessageId = messageId;
        AgentKey = string.IsNullOrWhiteSpace(agentKey) ? throw new ArgumentException("Agent key is required.", nameof(agentKey)) : agentKey.Trim();
        Provider = string.IsNullOrWhiteSpace(provider) ? "internal" : provider.Trim();
        ModelKey = string.IsNullOrWhiteSpace(modelKey) ? "default" : modelKey.Trim();
        EventType = string.IsNullOrWhiteSpace(eventType) ? "message" : eventType.Trim();
        InputTokens = Math.Max(0, inputTokens);
        OutputTokens = Math.Max(0, outputTokens);
        EstimatedCostUsd = estimatedCostUsd < 0 ? 0 : estimatedCostUsd;
        LatencyMs = Math.Max(0, latencyMs);
        OccurredAtUtc = DateTime.UtcNow;
    }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid ActorId { get; private set; }
    public Guid ConversationId { get; private set; }
    public Guid? MessageId { get; private set; }
    public string AgentKey { get; private set; } = string.Empty;
    public string Provider { get; private set; } = "internal";
    public string ModelKey { get; private set; } = "default";
    public string EventType { get; private set; } = "message";
    public int InputTokens { get; private set; }
    public int OutputTokens { get; private set; }
    public decimal EstimatedCostUsd { get; private set; }
    public int LatencyMs { get; private set; }
    public DateTime OccurredAtUtc { get; private set; }
}
