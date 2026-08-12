using Xenia.Application.Adapters.Interfaces;

namespace Xenia.Infrastructure.Platform;

/// <summary>
/// Noop implementation of <see cref="INotificationAdapter"/>.
/// Returns honest unavailable results. Never reports false success.
/// </summary>
internal sealed class UnavailableNotificationAdapter : INotificationAdapter
{
    private const string UnconfiguredMessage =
        "Notification adapter is not configured. Wire a real INotificationAdapter for production.";

    public bool IsConfigured => false;

    public Task<NotificationSubmitResult> SubmitNotificationAsync(
        NotificationRequest request, CancellationToken ct = default)
        => Task.FromResult(new NotificationSubmitResult(
            IsSubmitted: false,
            IsAvailable: false,
            DeliveryReference: null,
            Message: UnconfiguredMessage));
}
