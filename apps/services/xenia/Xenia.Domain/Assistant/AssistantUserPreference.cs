using Xenia.Domain.Common;

namespace Xenia.Domain.Assistant;

public sealed class AssistantUserPreference : AuditableEntityBase
{
    private AssistantUserPreference() { }

    public AssistantUserPreference(Guid id, Guid tenantId, Guid actorId)
    {
        Id = id == Guid.Empty ? throw new ArgumentException("Preference id must not be empty.", nameof(id)) : id;
        TenantId = tenantId == Guid.Empty ? throw new ArgumentException("Tenant id must not be empty.", nameof(tenantId)) : tenantId;
        ActorId = actorId == Guid.Empty ? throw new ArgumentException("Actor id must not be empty.", nameof(actorId)) : actorId;
        DefaultAgentKey = "generic";
        ContextHintsEnabled = true;
        PreferencesJson = "{}";
    }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid ActorId { get; private set; }
    public string DefaultAgentKey { get; private set; } = "generic";
    public bool ContextHintsEnabled { get; private set; }
    public string PreferencesJson { get; private set; } = "{}";
    public uint RowVersion { get; private set; }

    public void Update(string defaultAgentKey, bool contextHintsEnabled, string preferencesJson)
    {
        DefaultAgentKey = string.IsNullOrWhiteSpace(defaultAgentKey) ? "generic" : defaultAgentKey.Trim();
        ContextHintsEnabled = contextHintsEnabled;
        PreferencesJson = string.IsNullOrWhiteSpace(preferencesJson) ? "{}" : preferencesJson;
    }
}
