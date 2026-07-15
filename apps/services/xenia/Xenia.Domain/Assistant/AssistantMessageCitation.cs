using Xenia.Domain.Common;

namespace Xenia.Domain.Assistant;

public sealed class AssistantMessageCitation : AuditableEntityBase
{
    public const int SourceTypeMaxLength = 50;
    public const int SourceIdMaxLength = 200;
    public const int LabelMaxLength = 300;
    public const int UrlMaxLength = 1000;

    private AssistantMessageCitation() { }

    public AssistantMessageCitation(
        Guid id,
        Guid messageId,
        Guid tenantId,
        string sourceType,
        string sourceId,
        string label,
        string? url)
    {
        Id = id == Guid.Empty ? throw new ArgumentException("Citation id must not be empty.", nameof(id)) : id;
        MessageId = messageId == Guid.Empty ? throw new ArgumentException("Message id must not be empty.", nameof(messageId)) : messageId;
        TenantId = tenantId == Guid.Empty ? throw new ArgumentException("Tenant id must not be empty.", nameof(tenantId)) : tenantId;
        SourceType = Required(sourceType, nameof(sourceType), SourceTypeMaxLength);
        SourceId = Required(sourceId, nameof(sourceId), SourceIdMaxLength);
        Label = Required(label, nameof(label), LabelMaxLength);
        Url = string.IsNullOrWhiteSpace(url) ? null : url.Trim();
    }

    public Guid Id { get; private set; }
    public Guid MessageId { get; private set; }
    public Guid TenantId { get; private set; }
    public string SourceType { get; private set; } = string.Empty;
    public string SourceId { get; private set; } = string.Empty;
    public string Label { get; private set; } = string.Empty;
    public string? Url { get; private set; }

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
