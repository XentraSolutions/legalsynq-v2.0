using BuildingBlocks.Domain;

namespace Liens.Domain.Entities;

public class SellingBuyerAccessLink : AuditableEntity
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid LienId { get; private set; }
    public Guid SellerOrgId { get; private set; }
    public Guid BuyerOrgId { get; private set; }
    public Guid BuyerContactId { get; private set; }
    public string Token { get; private set; } = string.Empty;
    public string Purpose { get; private set; } = string.Empty;
    public string IdempotencyKey { get; private set; } = string.Empty;
    public DateTime ExpiresAtUtc { get; private set; }
    public DateTime? LastAccessedAtUtc { get; private set; }
    public DateTime? RevokedAtUtc { get; private set; }
    public Guid? NotificationId { get; private set; }
    public string? NotificationStatus { get; private set; }
    public DateTime? NotificationSubmittedAtUtc { get; private set; }

    private SellingBuyerAccessLink() { }

    public static SellingBuyerAccessLink Create(
        Guid tenantId,
        Guid lienId,
        Guid sellerOrgId,
        Guid buyerOrgId,
        Guid buyerContactId,
        string token,
        string purpose,
        string idempotencyKey,
        DateTime expiresAtUtc,
        Guid createdByUserId)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("TenantId is required.", nameof(tenantId));
        if (lienId == Guid.Empty) throw new ArgumentException("LienId is required.", nameof(lienId));
        if (sellerOrgId == Guid.Empty) throw new ArgumentException("SellerOrgId is required.", nameof(sellerOrgId));
        if (buyerOrgId == Guid.Empty) throw new ArgumentException("BuyerOrgId is required.", nameof(buyerOrgId));
        if (buyerContactId == Guid.Empty) throw new ArgumentException("BuyerContactId is required.", nameof(buyerContactId));
        if (createdByUserId == Guid.Empty) throw new ArgumentException("CreatedByUserId is required.", nameof(createdByUserId));
        ArgumentException.ThrowIfNullOrWhiteSpace(token);
        ArgumentException.ThrowIfNullOrWhiteSpace(purpose);
        ArgumentException.ThrowIfNullOrWhiteSpace(idempotencyKey);

        if (expiresAtUtc <= DateTime.UtcNow)
            throw new ArgumentOutOfRangeException(nameof(expiresAtUtc), "Expiry must be in the future.");

        var now = DateTime.UtcNow;
        return new SellingBuyerAccessLink
        {
            Id = Guid.CreateVersion7(),
            TenantId = tenantId,
            LienId = lienId,
            SellerOrgId = sellerOrgId,
            BuyerOrgId = buyerOrgId,
            BuyerContactId = buyerContactId,
            Token = token.Trim(),
            Purpose = purpose.Trim(),
            IdempotencyKey = idempotencyKey.Trim(),
            ExpiresAtUtc = expiresAtUtc,
            CreatedByUserId = createdByUserId,
            UpdatedByUserId = createdByUserId,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };
    }

    public void MarkNotificationSubmitted(Guid? notificationId, string status)
    {
        if (string.IsNullOrWhiteSpace(status))
            throw new ArgumentException("Notification status is required.", nameof(status));

        NotificationId = notificationId;
        NotificationStatus = status.Trim();
        NotificationSubmittedAtUtc = DateTime.UtcNow;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void MarkAccessed()
    {
        LastAccessedAtUtc = DateTime.UtcNow;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void Revoke(Guid updatedByUserId)
    {
        if (updatedByUserId == Guid.Empty)
            throw new ArgumentException("UpdatedByUserId is required.", nameof(updatedByUserId));

        RevokedAtUtc = DateTime.UtcNow;
        UpdatedByUserId = updatedByUserId;
        UpdatedAtUtc = DateTime.UtcNow;
    }
}
