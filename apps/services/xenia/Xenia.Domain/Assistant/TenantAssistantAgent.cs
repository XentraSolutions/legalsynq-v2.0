using Xenia.Domain.Common;

namespace Xenia.Domain.Assistant;

public sealed class TenantAssistantAgent : AuditableEntityBase
{
    private TenantAssistantAgent() { }

    public TenantAssistantAgent(Guid id, Guid tenantId, string agentKey)
    {
        Id = id == Guid.Empty ? throw new ArgumentException("Tenant agent id must not be empty.", nameof(id)) : id;
        TenantId = tenantId == Guid.Empty ? throw new ArgumentException("Tenant id must not be empty.", nameof(tenantId)) : tenantId;
        AgentKey = string.IsNullOrWhiteSpace(agentKey)
            ? throw new ArgumentException("Agent key is required.", nameof(agentKey))
            : agentKey.Trim();
        Enabled = true;
        ConfigurationJson = "{}";
    }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public string AgentKey { get; private set; } = string.Empty;
    public bool Enabled { get; private set; }
    public string ConfigurationJson { get; private set; } = "{}";
    public Guid? UpdatedBy { get; private set; }
    public uint RowVersion { get; private set; }

    public void SetEnabled(bool enabled, Guid? actorId)
    {
        Enabled = enabled;
        UpdatedBy = actorId;
    }

    public void UpdateConfiguration(string configurationJson, Guid? actorId)
    {
        ConfigurationJson = string.IsNullOrWhiteSpace(configurationJson) ? "{}" : configurationJson;
        UpdatedBy = actorId;
    }
}
