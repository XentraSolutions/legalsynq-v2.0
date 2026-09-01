using BuildingBlocks.Domain;
using Liens.Domain.Enums;

namespace Liens.Domain.Entities;

public class SellingPortalMessage : AuditableEntity
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid LienId { get; private set; }
    public Guid SellerOrgId { get; private set; }
    public Guid BuyerOrgId { get; private set; }
    public Guid BuyerContactId { get; private set; }
    public Guid AccessLinkId { get; private set; }
    public string SenderType { get; private set; } = string.Empty;
    public string SenderName { get; private set; } = string.Empty;
    public string? SenderEmail { get; private set; }
    public string Message { get; private set; } = string.Empty;

    private SellingPortalMessage() { }

    public static SellingPortalMessage Create(
        Guid tenantId,
        Guid lienId,
        Guid sellerOrgId,
        Guid buyerOrgId,
        Guid buyerContactId,
        Guid accessLinkId,
        string senderType,
        string senderName,
        string? senderEmail,
        string message,
        Guid createdByUserId)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("TenantId is required.", nameof(tenantId));
        if (lienId == Guid.Empty) throw new ArgumentException("LienId is required.", nameof(lienId));
        if (sellerOrgId == Guid.Empty) throw new ArgumentException("SellerOrgId is required.", nameof(sellerOrgId));
        if (buyerOrgId == Guid.Empty) throw new ArgumentException("BuyerOrgId is required.", nameof(buyerOrgId));
        if (buyerContactId == Guid.Empty) throw new ArgumentException("BuyerContactId is required.", nameof(buyerContactId));
        if (accessLinkId == Guid.Empty) throw new ArgumentException("AccessLinkId is required.", nameof(accessLinkId));
        if (createdByUserId == Guid.Empty) throw new ArgumentException("CreatedByUserId is required.", nameof(createdByUserId));
        ArgumentException.ThrowIfNullOrWhiteSpace(senderType);
        ArgumentException.ThrowIfNullOrWhiteSpace(senderName);
        ArgumentNullException.ThrowIfNull(message);

        if (!SellingPortalMessageSenderType.All.Contains(senderType))
            throw new ArgumentException($"Invalid sender type: '{senderType}'.", nameof(senderType));

        var now = DateTime.UtcNow;
        return new SellingPortalMessage
        {
            Id = Guid.CreateVersion7(),
            TenantId = tenantId,
            LienId = lienId,
            SellerOrgId = sellerOrgId,
            BuyerOrgId = buyerOrgId,
            BuyerContactId = buyerContactId,
            AccessLinkId = accessLinkId,
            SenderType = senderType.Trim(),
            SenderName = senderName.Trim(),
            SenderEmail = senderEmail?.Trim(),
            Message = message.Trim(),
            CreatedByUserId = createdByUserId,
            UpdatedByUserId = createdByUserId,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };
    }
}
