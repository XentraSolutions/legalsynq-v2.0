using BuildingBlocks.Domain;

namespace Liens.Domain.Entities;

public class SettlementPaymentDetail : AuditableEntity
{
    public Guid    Id            { get; private set; }
    public Guid    TenantId      { get; private set; }
    public Guid    CaseId        { get; private set; }
    public Guid    LienId        { get; private set; }
    public int     PaymentNumber { get; private set; }
    public decimal Amount        { get; private set; }
    public DateOnly? PaymentDate { get; private set; }
    public string? Payee         { get; private set; }
    public string? CheckNumber   { get; private set; }
    public string? Note          { get; private set; }
    public bool    IsDeleted     { get; private set; }

    private SettlementPaymentDetail() { }

    public static SettlementPaymentDetail Create(
        Guid tenantId, Guid caseId, Guid lienId,
        int paymentNumber, decimal amount, Guid createdByUserId,
        DateOnly? paymentDate = null,
        string? payee = null,
        string? checkNumber = null,
        string? note = null)
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
            PaymentNumber   = paymentNumber,
            Amount          = amount,
            PaymentDate     = paymentDate,
            Payee           = payee?.Trim(),
            CheckNumber     = checkNumber?.Trim(),
            Note            = note?.Trim(),
            IsDeleted       = false,
            CreatedByUserId = createdByUserId,
            UpdatedByUserId = createdByUserId,
            CreatedAtUtc    = now,
            UpdatedAtUtc    = now,
        };
    }

    public void SoftDelete(Guid updatedByUserId)
    {
        IsDeleted       = true;
        UpdatedByUserId = updatedByUserId;
        UpdatedAtUtc    = DateTime.UtcNow;
    }
}
