using Xenia.Domain.Common;

namespace Xenia.Domain.Assistant;

public sealed class AssistantConversation : AuditableEntityBase
{
    public const int TitleMaxLength = 200;
    public const int AgentKeyMaxLength = 100;
    public const int AgentVersionMaxLength = 50;
    public const int SourceMaxLength = 50;

    private AssistantConversation() { }

    public AssistantConversation(
        Guid id,
        Guid tenantId,
        Guid actorId,
        string agentKey,
        string agentVersion,
        string title,
        string source,
        string contextJson)
    {
        Id = id == Guid.Empty ? throw new ArgumentException("Conversation id must not be empty.", nameof(id)) : id;
        TenantId = tenantId == Guid.Empty ? throw new ArgumentException("Tenant id must not be empty.", nameof(tenantId)) : tenantId;
        ActorId = actorId == Guid.Empty ? throw new ArgumentException("Actor id must not be empty.", nameof(actorId)) : actorId;
        AgentKey = Required(agentKey, nameof(agentKey), AgentKeyMaxLength);
        AgentVersion = Required(agentVersion, nameof(agentVersion), AgentVersionMaxLength);
        Title = Required(title, nameof(title), TitleMaxLength);
        Source = Required(source, nameof(source), SourceMaxLength);
        ContextJson = string.IsNullOrWhiteSpace(contextJson) ? "{}" : contextJson;
        Status = AssistantConversationStatus.Active;
    }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid ActorId { get; private set; }
    public string AgentKey { get; private set; } = string.Empty;
    public string AgentVersion { get; private set; } = string.Empty;
    public string Title { get; private set; } = string.Empty;
    public string Source { get; private set; } = string.Empty;
    public string ContextJson { get; private set; } = "{}";
    public AssistantConversationStatus Status { get; private set; }
    public DateTime? ArchivedAtUtc { get; private set; }
    public DateTime? LastMessageAtUtc { get; private set; }
    public uint RowVersion { get; private set; }

    public void Rename(string title)
        => Title = Required(title, nameof(title), TitleMaxLength);

    public void Touch(DateTime utcNow)
        => LastMessageAtUtc = utcNow;

    public void Archive(DateTime utcNow)
    {
        Status = AssistantConversationStatus.Archived;
        ArchivedAtUtc = utcNow;
    }

    private static string Required(string value, string paramName, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException($"{paramName} is required.", paramName);

        var trimmed = value.Trim();
        if (trimmed.Length > maxLength)
            throw new ArgumentOutOfRangeException(paramName, $"{paramName} must be {maxLength} characters or fewer.");

        return trimmed;
    }
}
