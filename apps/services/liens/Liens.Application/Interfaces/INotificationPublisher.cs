namespace Liens.Application.Interfaces;

public interface INotificationPublisher
{
    Task PublishAsync(
        string notificationType,
        Guid tenantId,
        Dictionary<string, string> data,
        CancellationToken ct = default);

    Task<NotificationEmailSendResult> SendEmailAsync(
        string notificationType,
        Guid tenantId,
        string recipientEmail,
        string subject,
        string body,
        Dictionary<string, string> metadata,
        CancellationToken ct = default,
        NotificationEmailSendOptions? options = null);
}

public sealed record NotificationEmailSendOptions(
    string? IdempotencyKey = null,
    string? TemplateKey = null,
    Dictionary<string, string>? TemplateData = null,
    string? RequestedBy = null,
    bool? BrandedRendering = null);

public sealed record NotificationEmailSendResult(
    Guid? NotificationId,
    string Status,
    bool BlockedByPolicy,
    string? BlockedReasonCode,
    string? FailureCategory,
    string? LastErrorMessage)
{
    public bool Succeeded => string.Equals(Status, "sent", StringComparison.OrdinalIgnoreCase);
}
