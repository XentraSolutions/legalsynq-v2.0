namespace Liens.Application.Interfaces;

public static class SellingAccessLinkPurposes
{
    public const string ConfirmSaleBuyerResponse = "ConfirmSale";
    public const string ConfirmSaleSellerView = "ConfirmSaleSellerView";
}

public interface ISellingBuyerAccessLinkService
{
    /// <summary>
    /// Creates a buyer access grant for the supplied selling lien. The raw token is
    /// returned only for a newly-created link; replay responses intentionally omit it.
    /// </summary>
    Task<SellingBuyerAccessLinkResult> CreateAsync(
        Guid tenantId,
        Guid lienId,
        Guid sellerOrgId,
        Guid buyerOrgId,
        Guid buyerContactId,
        Guid actingUserId,
        string route,
        string idempotencyKey,
        TimeSpan ttl,
        CancellationToken ct = default);

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

    Task<SellingBuyerAccessLinkResult> CreateOrGetForConfirmSaleSellerViewAsync(
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
    string? Token,
    string BuyerPortalUrl,
    DateTime ExpiresAtUtc,
    bool AlreadyExisted,
    Guid? NotificationId,
    string? NotificationStatus,
    DateTime? NotificationSubmittedAtUtc)
{
    public string PublicPortalUrl => BuyerPortalUrl;
}
