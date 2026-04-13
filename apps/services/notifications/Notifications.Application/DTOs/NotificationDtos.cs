namespace Notifications.Application.DTOs;

public class SubmitNotificationDto
{
    public string Channel { get; set; } = string.Empty;
    public object Recipient { get; set; } = new();
    public object Message { get; set; } = new();
    public object? Metadata { get; set; }
    public string? IdempotencyKey { get; set; }
    public string? TemplateKey { get; set; }
    public Dictionary<string, string>? TemplateData { get; set; }
    public string? ProductType { get; set; }
    public bool? BrandedRendering { get; set; }
    public bool? OverrideSuppression { get; set; }
    public string? OverrideReason { get; set; }
}

public class NotificationResultDto
{
    public Guid Id { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? ProviderUsed { get; set; }
    public bool PlatformFallbackUsed { get; set; }
    public bool BlockedByPolicy { get; set; }
    public string? BlockedReasonCode { get; set; }
    public bool OverrideUsed { get; set; }
    public string? FailureCategory { get; set; }
    public string? LastErrorMessage { get; set; }
}

public class NotificationDto
{
    public Guid Id { get; set; }
    public Guid? TenantId { get; set; }
    public string Channel { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string RecipientJson { get; set; } = string.Empty;
    public string MessageJson { get; set; } = string.Empty;
    public string? MetadataJson { get; set; }
    public string? IdempotencyKey { get; set; }
    public string? ProviderUsed { get; set; }
    public string? FailureCategory { get; set; }
    public string? LastErrorMessage { get; set; }
    public Guid? TemplateId { get; set; }
    public Guid? TemplateVersionId { get; set; }
    public string? TemplateKey { get; set; }
    public string? RenderedSubject { get; set; }
    public string? RenderedBody { get; set; }
    public string? RenderedText { get; set; }
    public string? ProviderOwnershipMode { get; set; }
    public Guid? ProviderConfigId { get; set; }
    public bool PlatformFallbackUsed { get; set; }
    public bool BlockedByPolicy { get; set; }
    public string? BlockedReasonCode { get; set; }
    public bool OverrideUsed { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
