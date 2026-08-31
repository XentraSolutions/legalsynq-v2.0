using BuildingBlocks.Domain;

namespace Liens.Domain.Entities;

public sealed class SellingPortalMessageAttachment : AuditableEntity
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid LienId { get; private set; }
    public Guid SellerOrgId { get; private set; }
    public Guid BuyerOrgId { get; private set; }
    public Guid BuyerContactId { get; private set; }
    public Guid AccessLinkId { get; private set; }
    public Guid MessageId { get; private set; }
    public Guid DocumentId { get; private set; }
    public string FileName { get; private set; } = string.Empty;
    public string ContentType { get; private set; } = string.Empty;
    public long FileSizeBytes { get; private set; }

    private SellingPortalMessageAttachment() { }

    public static SellingPortalMessageAttachment Create(
        SellingPortalMessage message,
        Guid documentId,
        string fileName,
        string contentType,
        long fileSizeBytes,
        Guid createdByUserId)
    {
        if (message.Id == Guid.Empty) throw new ArgumentException("MessageId is required.", nameof(message));
        if (documentId == Guid.Empty) throw new ArgumentException("DocumentId is required.", nameof(documentId));
        if (createdByUserId == Guid.Empty) throw new ArgumentException("CreatedByUserId is required.", nameof(createdByUserId));
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);

        var now = DateTime.UtcNow;
        return new SellingPortalMessageAttachment
        {
            Id = Guid.CreateVersion7(),
            TenantId = message.TenantId,
            LienId = message.LienId,
            SellerOrgId = message.SellerOrgId,
            BuyerOrgId = message.BuyerOrgId,
            BuyerContactId = message.BuyerContactId,
            AccessLinkId = message.AccessLinkId,
            MessageId = message.Id,
            DocumentId = documentId,
            FileName = fileName.Trim(),
            ContentType = string.IsNullOrWhiteSpace(contentType)
                ? "application/octet-stream"
                : contentType.Trim(),
            FileSizeBytes = fileSizeBytes,
            CreatedByUserId = createdByUserId,
            UpdatedByUserId = createdByUserId,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };
    }
}
