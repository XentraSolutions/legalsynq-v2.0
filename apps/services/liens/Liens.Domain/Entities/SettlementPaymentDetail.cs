using BuildingBlocks.Domain;

namespace Liens.Domain.Entities;

public class SettlementPaymentDetail : AuditableEntity
{
    public const string PostedStatus = "Posted";
    public const string VoidedStatus = "Voided";

    public Guid    Id            { get; private set; }
    public Guid    TenantId      { get; private set; }
    public Guid    CaseId        { get; private set; }
    public Guid    LienId        { get; private set; }
    public Guid?   ReceiptId     { get; private set; }
    public int     PaymentNumber { get; private set; }
    public decimal Amount        { get; private set; }
    public DateOnly? PaymentDate { get; private set; }
    public string? Payee         { get; private set; }
    public string? CheckNumber   { get; private set; }
    public string? PaymentMethod { get; private set; }
    public string? SettlementType { get; private set; }
    public string? SettlementStatus { get; private set; }
    public string? DetailsContext { get; private set; }
    public string? Note          { get; private set; }
    public string  PostingStatus { get; private set; } = PostedStatus;
    public DateTime? VoidedAtUtc { get; private set; }
    public Guid?   VoidedByUserId { get; private set; }
    public string? VoidReason    { get; private set; }
    public bool    IsDeleted     { get; private set; }

    private SettlementPaymentDetail() { }

    public static SettlementPaymentDetail Create(
        Guid tenantId, Guid caseId, Guid lienId,
        int paymentNumber, decimal amount, Guid createdByUserId,
        DateOnly? paymentDate = null,
        string? payee = null,
        string? checkNumber = null,
        string? note = null,
        Guid? receiptId = null,
        string? paymentMethod = null,
        string? settlementType = null,
        string? settlementStatus = null,
        string? detailsContext = null)
    {
        if (tenantId == Guid.Empty)   throw new ArgumentException("TenantId is required.", nameof(tenantId));
        if (caseId == Guid.Empty)     throw new ArgumentException("CaseId is required.", nameof(caseId));
        if (lienId == Guid.Empty)     throw new ArgumentException("LienId is required.", nameof(lienId));
        if (createdByUserId == Guid.Empty) throw new ArgumentException("CreatedByUserId is required.", nameof(createdByUserId));

        var now = DateTime.UtcNow;
        return new SettlementPaymentDetail
        {
            Id              = Guid.CreateVersion7(),
            TenantId        = tenantId,
            CaseId          = caseId,
            LienId          = lienId,
            ReceiptId       = receiptId,
            PaymentNumber   = paymentNumber,
            Amount          = amount,
            PaymentDate     = paymentDate,
            Payee           = payee?.Trim(),
            CheckNumber     = checkNumber?.Trim(),
            PaymentMethod   = paymentMethod?.Trim(),
            SettlementType  = settlementType?.Trim(),
            SettlementStatus = settlementStatus?.Trim(),
            DetailsContext  = detailsContext?.Trim(),
            Note            = note?.Trim(),
            PostingStatus   = PostedStatus,
            IsDeleted       = false,
            CreatedByUserId = createdByUserId,
            UpdatedByUserId = createdByUserId,
            CreatedAtUtc    = now,
            UpdatedAtUtc    = now,
        };
    }

    public void Update(
        decimal amount,
        DateOnly paymentDate,
        string? checkNumber,
        string? note,
        Guid updatedByUserId)
    {
        if (updatedByUserId == Guid.Empty)
            throw new ArgumentException("UpdatedByUserId is required.", nameof(updatedByUserId));
        if (PostingStatus == VoidedStatus)
            throw new InvalidOperationException("A voided payment cannot be edited.");

        Amount          = amount;
        PaymentDate     = paymentDate;
        CheckNumber     = string.IsNullOrWhiteSpace(checkNumber) ? null : checkNumber.Trim();
        Note            = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        UpdatedByUserId = updatedByUserId;
        UpdatedAtUtc    = DateTime.UtcNow;
    }

    public void UpdateClassification(
        string? paymentMethod,
        string? settlementType,
        string? settlementStatus,
        string? detailsContext,
        Guid updatedByUserId)
    {
        if (updatedByUserId == Guid.Empty)
            throw new ArgumentException("UpdatedByUserId is required.", nameof(updatedByUserId));
        if (PostingStatus == VoidedStatus)
            throw new InvalidOperationException("A voided payment cannot be edited.");

        PaymentMethod = paymentMethod?.Trim();
        SettlementType = settlementType?.Trim();
        SettlementStatus = settlementStatus?.Trim();
        DetailsContext = detailsContext?.Trim();
        UpdatedByUserId = updatedByUserId;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void Void(Guid voidedByUserId, string reason)
    {
        if (voidedByUserId == Guid.Empty)
            throw new ArgumentException("VoidedByUserId is required.", nameof(voidedByUserId));
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        if (PostingStatus == VoidedStatus)
            throw new InvalidOperationException("Payment is already voided.");

        PostingStatus = VoidedStatus;
        VoidedAtUtc = DateTime.UtcNow;
        VoidedByUserId = voidedByUserId;
        VoidReason = reason.Trim();
        UpdatedByUserId = voidedByUserId;
        UpdatedAtUtc = VoidedAtUtc.Value;
    }

    public void SoftDelete(Guid updatedByUserId)
    {
        IsDeleted       = true;
        UpdatedByUserId = updatedByUserId;
        UpdatedAtUtc    = DateTime.UtcNow;
    }
}
