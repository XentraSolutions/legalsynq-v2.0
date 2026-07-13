using Xenia.Domain.Common;

namespace Xenia.Domain.Assistant;

public sealed class AssistantToolInvocation : AuditableEntityBase
{
    public const int ToolKeyMaxLength = 100;
    public const int StatusMaxLength = 50;
    public const int SafeErrorMaxLength = 1000;

    private AssistantToolInvocation() { }

    public AssistantToolInvocation(
        Guid id,
        Guid conversationId,
        Guid? messageId,
        Guid tenantId,
        Guid actorId,
        string agentKey,
        string toolKey,
        string inputJson)
    {
        Id = id == Guid.Empty ? throw new ArgumentException("Tool invocation id must not be empty.", nameof(id)) : id;
        ConversationId = conversationId == Guid.Empty ? throw new ArgumentException("Conversation id must not be empty.", nameof(conversationId)) : conversationId;
        MessageId = messageId;
        TenantId = tenantId == Guid.Empty ? throw new ArgumentException("Tenant id must not be empty.", nameof(tenantId)) : tenantId;
        ActorId = actorId == Guid.Empty ? throw new ArgumentException("Actor id must not be empty.", nameof(actorId)) : actorId;
        AgentKey = string.IsNullOrWhiteSpace(agentKey) ? throw new ArgumentException("Agent key is required.", nameof(agentKey)) : agentKey.Trim();
        ToolKey = string.IsNullOrWhiteSpace(toolKey) ? throw new ArgumentException("Tool key is required.", nameof(toolKey)) : toolKey.Trim();
        InputJson = string.IsNullOrWhiteSpace(inputJson) ? "{}" : inputJson;
        Status = "pending";
        StartedAtUtc = DateTime.UtcNow;
    }

    public Guid Id { get; private set; }
    public Guid ConversationId { get; private set; }
    public Guid? MessageId { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid ActorId { get; private set; }
    public string AgentKey { get; private set; } = string.Empty;
    public string ToolKey { get; private set; } = string.Empty;
    public string InputJson { get; private set; } = "{}";
    public string? OutputJson { get; private set; }
    public string Status { get; private set; } = "pending";
    public bool ConfirmationRequired { get; private set; }
    public DateTime StartedAtUtc { get; private set; }
    public DateTime? CompletedAtUtc { get; private set; }
    public string? SafeError { get; private set; }
    public uint RowVersion { get; private set; }

    public void MarkConfirmationRequired()
        => ConfirmationRequired = true;

    public void Complete(string outputJson)
    {
        OutputJson = string.IsNullOrWhiteSpace(outputJson) ? "{}" : outputJson;
        Status = "completed";
        CompletedAtUtc = DateTime.UtcNow;
    }

    public void Fail(string safeError)
    {
        Status = "failed";
        SafeError = string.IsNullOrWhiteSpace(safeError) ? "Tool execution failed." : safeError.Trim();
        CompletedAtUtc = DateTime.UtcNow;
    }
}
