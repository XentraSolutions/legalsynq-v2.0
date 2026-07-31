using BuildingBlocks.Domain;

namespace Liens.Domain.Entities;

public class LienSettlement : AuditableEntity
{
    public Guid    Id            { get; private set; }
    public Guid    TenantId      { get; private set; }
    public Guid    CaseId        { get; private set; }
    public Guid    LienId        { get; private set; }
    public int     PaymentNumber { get; private set; }
    public decimal Amount        { get; private set; }
    public DateOnly? SettlementDate { get; private set; }
    public string  Status        { get; private set; } = "Pending";
    public string? Note          { get; private set; }
    public bool    IsDeleted     { get; private set; }

    private LienSettlement() { }

    public static LienSettlement Create(
        Guid tenantId, Guid caseId, Guid lienId,
        int paymentNumber, decimal amount, Guid createdByUserId,
        string? status = null, string? note = null,
        DateOnly? settlementDate = null)
    {
        if (tenantId == Guid.Empty)   throw new ArgumentException("TenantId is required.", nameof(tenantId));
        if (caseId == Guid.Empty)     throw new ArgumentException("CaseId is required.", nameof(caseId));
        if (lienId == Guid.Empty)     throw new ArgumentException("LienId is required.", nameof(lienId));
        if (createdByUserId == Guid.Empty) throw new ArgumentException("CreatedByUserId is required.", nameof(createdByUserId));

        var now = DateTime.UtcNow;
        return new LienSettlement
        {
            Id              = Guid.CreateVersion7(),
            TenantId        = tenantId,
            CaseId          = caseId,
            LienId          = lienId,
            PaymentNumber   = paymentNumber,
            Amount          = amount,
            SettlementDate  = settlementDate,
            Status          = (status ?? "Pending").Trim(),
            Note            = note?.Trim(),
            IsDeleted       = false,
            CreatedByUserId = createdByUserId,
            UpdatedByUserId = createdByUserId,
            CreatedAtUtc    = now,
            UpdatedAtUtc    = now,
        };
    }

    public void Update(
        int paymentNumber,
        decimal amount,
        Guid updatedByUserId,
        string? status = null,
        string? note = null,
        DateOnly? settlementDate = null)
    {
        PaymentNumber   = paymentNumber;
        Amount          = amount;
        SettlementDate  = settlementDate;
        Status          = (status ?? Status).Trim();
        Note            = note?.Trim();
        UpdatedByUserId = updatedByUserId;
        UpdatedAtUtc    = DateTime.UtcNow;
    }

    public void SoftDelete(Guid updatedByUserId)
    {
        IsDeleted       = true;
        UpdatedByUserId = updatedByUserId;
        UpdatedAtUtc    = DateTime.UtcNow;
    }
}
