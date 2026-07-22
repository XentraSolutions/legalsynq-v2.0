namespace Liens.Application.Interfaces;

public interface ISellingBuyerAccessLinkService
{
    Task<SellingBuyerAccessLinkResult> CreateOrGetForConfirmSaleAsync(
        Guid tenantId,
        Guid lienId,
        Guid sellerOrgId,
        Guid buyerOrgId,
        Guid buyerContactId,
        Guid actingUserId,
        string idempotencyKey,
        TimeSpan ttl,
        CancellationToken ct = default);

    Task MarkNotificationSubmittedAsync(
        Guid tenantId,
        Guid accessLinkId,
        Guid? notificationId,
        string notificationStatus,
        CancellationToken ct = default);
}

public sealed record SellingBuyerAccessLinkResult(
    Guid Id,
    string Token,
    string BuyerPortalUrl,
    DateTime ExpiresAtUtc,
    bool AlreadyExisted,
    Guid? NotificationId,
    string? NotificationStatus,
    DateTime? NotificationSubmittedAtUtc);
