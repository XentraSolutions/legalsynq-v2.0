namespace Xenia.Application.Adapters.Interfaces;

/// <summary>
/// Platform-neutral contract for submitting notification requests through
/// the platform's notification infrastructure.
/// </summary>
public interface INotificationAdapter
{
    /// <summary>Whether this adapter is configured for the current environment.</summary>
    bool IsConfigured { get; }

    /// <summary>
    /// Submits a notification request to the platform notification service.
    /// Returns a delivery reference if accepted, or an unavailable result.
    /// </summary>
    Task<NotificationSubmitResult> SubmitNotificationAsync(
        NotificationRequest request,
        CancellationToken ct = default);
}

public sealed record NotificationRequest(
    Guid TenantId,
    Guid? RecipientUserId,
    string RecipientEmail,
    string TemplateKey,
    IReadOnlyDictionary<string, string> TemplateData,
    string Channel = "email");

public sealed record NotificationSubmitResult(bool IsSubmitted, bool IsAvailable, string? DeliveryReference, string? Message);
