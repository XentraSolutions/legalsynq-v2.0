using System.Security.Cryptography;
using System.Text;
using BuildingBlocks.Domain;
using Liens.Domain.Enums;

namespace Liens.Domain.Entities;

public class SellingBuyerAccessLink : AuditableEntity
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid LienId { get; private set; }
    public Guid SellerOrgId { get; private set; }
    public Guid BuyerOrgId { get; private set; }
    public Guid BuyerContactId { get; private set; }
    public string TokenHash { get; private set; } = string.Empty;
    public string Purpose { get; private set; } = string.Empty;
    public string Route { get; private set; } = string.Empty;
    public string IdempotencyKey { get; private set; } = string.Empty;
    public DateTime ExpiresAtUtc { get; private set; }
    public DateTime? LastAccessedAtUtc { get; private set; }
    public DateTime? RevokedAtUtc { get; private set; }
    public Guid? NotificationId { get; private set; }
    public string? NotificationStatus { get; private set; }
    public DateTime? NotificationSubmittedAtUtc { get; private set; }
    public string? ResponseStatus { get; private set; }
    public decimal? ResponseAmount { get; private set; }
    public string? ResponseNotes { get; private set; }
    public DateTime? RespondedAtUtc { get; private set; }
    public string? ResponseIdempotencyKey { get; private set; }
    public Guid? AccountActivatedUserId { get; private set; }
    public string? AccountActivatedEmail { get; private set; }
    public DateTime? AccountActivatedAtUtc { get; private set; }

    private SellingBuyerAccessLink() { }

    public static SellingBuyerAccessLink Create(
        Guid tenantId,
        Guid lienId,
        Guid sellerOrgId,
        Guid buyerOrgId,
        Guid buyerContactId,
        string token,
        string purpose,
        string route,
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
        ArgumentException.ThrowIfNullOrWhiteSpace(route);
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
            TokenHash = ComputeTokenHash(token),
            Purpose = purpose.Trim(),
            Route = route.Trim(),
            IdempotencyKey = idempotencyKey.Trim(),
            ExpiresAtUtc = expiresAtUtc,
            CreatedByUserId = createdByUserId,
            UpdatedByUserId = createdByUserId,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };
    }

    /// <summary>
    /// Produces the canonical SHA-256 digest used for public buyer-link lookups.
    /// The raw token is intentionally never retained on the entity.
    /// </summary>
    public static string ComputeTokenHash(string token)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);

        var bytes = Encoding.UTF8.GetBytes(token.Trim());
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
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

    public void RecordAccountActivation(Guid userId, string email)
    {
        if (userId == Guid.Empty)
            throw new ArgumentException("UserId is required.", nameof(userId));
        ArgumentException.ThrowIfNullOrWhiteSpace(email);

        AccountActivatedUserId = userId;
        AccountActivatedEmail = email.Trim();
        AccountActivatedAtUtc = DateTime.UtcNow;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void RecordResponse(
        string responseStatus,
        decimal? responseAmount,
        string? responseNotes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(responseStatus);
        if (!SellingBuyerResponseStatus.All.Contains(responseStatus))
            throw new ArgumentException($"Invalid buyer response status: '{responseStatus}'.", nameof(responseStatus));

        if (responseStatus == SellingBuyerResponseStatus.Accepted &&
            (!responseAmount.HasValue || responseAmount.Value <= 0m))
        {
            throw new ArgumentOutOfRangeException(nameof(responseAmount), "Accepted responses require a positive amount.");
        }

        if (responseStatus == SellingBuyerResponseStatus.Declined && responseAmount.HasValue)
            throw new ArgumentException("Declined responses cannot include a response amount.", nameof(responseAmount));

        ResponseStatus = responseStatus;
        ResponseAmount = responseAmount;
        ResponseNotes = responseNotes?.Trim();
        ResponseIdempotencyKey = null;
        RespondedAtUtc = DateTime.UtcNow;
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
