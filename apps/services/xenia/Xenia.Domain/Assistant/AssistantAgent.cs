using Xenia.Domain.Common;

namespace Xenia.Domain.Assistant;

public sealed class AssistantAgent : AuditableEntityBase
{
    public const int AgentKeyMaxLength = 100;
    public const int NameMaxLength = 200;
    public const int DescriptionMaxLength = 1000;
    public const int VersionMaxLength = 50;

    private AssistantAgent() { }

    public AssistantAgent(
        Guid id,
        string agentKey,
        string name,
        string description,
        string version,
        string systemPrompt,
        string allowedToolsJson,
        string requiredProductCodesJson,
        bool isEnabled = true)
    {
        Id = id == Guid.Empty ? throw new ArgumentException("Agent id must not be empty.", nameof(id)) : id;
        AgentKey = Required(agentKey, nameof(agentKey), AgentKeyMaxLength);
        Name = Required(name, nameof(name), NameMaxLength);
        Description = Required(description, nameof(description), DescriptionMaxLength);
        Version = Required(version, nameof(version), VersionMaxLength);
        SystemPrompt = Required(systemPrompt, nameof(systemPrompt));
        AllowedToolsJson = string.IsNullOrWhiteSpace(allowedToolsJson) ? "[]" : allowedToolsJson;
        RequiredProductCodesJson = string.IsNullOrWhiteSpace(requiredProductCodesJson) ? "[]" : requiredProductCodesJson;
        IsEnabled = isEnabled;
    }

    public Guid Id { get; private set; }
    public string AgentKey { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public string Version { get; private set; } = string.Empty;
    public string SystemPrompt { get; private set; } = string.Empty;
    public string AllowedToolsJson { get; private set; } = "[]";
    public string RequiredProductCodesJson { get; private set; } = "[]";
    public bool IsEnabled { get; private set; }
    public uint RowVersion { get; private set; }

    public void UpdateDefinition(
        string name,
        string description,
        string version,
        string systemPrompt,
        string allowedToolsJson,
        string requiredProductCodesJson)
    {
        Name = Required(name, nameof(name), NameMaxLength);
        Description = Required(description, nameof(description), DescriptionMaxLength);
        Version = Required(version, nameof(version), VersionMaxLength);
        SystemPrompt = Required(systemPrompt, nameof(systemPrompt));
        AllowedToolsJson = string.IsNullOrWhiteSpace(allowedToolsJson) ? "[]" : allowedToolsJson;
        RequiredProductCodesJson = string.IsNullOrWhiteSpace(requiredProductCodesJson) ? "[]" : requiredProductCodesJson;
    }

    public void Enable() => IsEnabled = true;
    public void Disable() => IsEnabled = false;

    private static string Required(string value, string paramName, int? maxLength = null)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException($"{paramName} is required.", paramName);

        var trimmed = value.Trim();
        if (maxLength.HasValue && trimmed.Length > maxLength.Value)
            throw new ArgumentOutOfRangeException(paramName, $"{paramName} must be {maxLength.Value} characters or fewer.");

        return trimmed;
    }
}
