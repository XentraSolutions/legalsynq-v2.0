using BuildingBlocks.Domain;

namespace Liens.Domain.Entities;

public class SellingPortfolioLien : AuditableEntity
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid PortfolioId { get; private set; }
    public Guid LienId { get; private set; }

    public string LienNumber { get; private set; } = string.Empty;
    public string? LienExternalId { get; private set; }
    public Guid? CaseId { get; private set; }
    public string? CaseExternalId { get; private set; }
    public Guid? FacilityId { get; private set; }
    public string LienType { get; private set; } = string.Empty;
    public string LienLifecycleStatus { get; private set; } = string.Empty;

    public decimal OriginalAmount { get; private set; }
    public decimal? CurrentBalance { get; private set; }
    public decimal? OfferPrice { get; private set; }
    public decimal? PurchasePrice { get; private set; }
    public decimal? PayoffAmount { get; private set; }

    public string? SubjectFirstName { get; private set; }
    public string? SubjectLastName { get; private set; }
    public string? Jurisdiction { get; private set; }
    public DateOnly? IncidentDate { get; private set; }
    public string? Description { get; private set; }

    private SellingPortfolioLien() { }

    public static SellingPortfolioLien CreateSnapshot(
        Guid tenantId,
        Guid portfolioId,
        Lien lien,
        string? caseExternalId,
        Guid createdByUserId)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("TenantId is required.", nameof(tenantId));
        if (portfolioId == Guid.Empty) throw new ArgumentException("PortfolioId is required.", nameof(portfolioId));
        if (createdByUserId == Guid.Empty) throw new ArgumentException("CreatedByUserId is required.", nameof(createdByUserId));
        ArgumentNullException.ThrowIfNull(lien);

        var now = DateTime.UtcNow;
        return new SellingPortfolioLien
        {
            Id = Guid.CreateVersion7(),
            TenantId = tenantId,
            PortfolioId = portfolioId,
            LienId = lien.Id,
            LienNumber = lien.LienNumber,
            LienExternalId = lien.ExternalReference,
            CaseId = lien.CaseId,
            CaseExternalId = caseExternalId,
            FacilityId = lien.FacilityId,
            LienType = lien.LienType,
            LienLifecycleStatus = lien.Status,
            OriginalAmount = lien.OriginalAmount,
            CurrentBalance = lien.CurrentBalance,
            OfferPrice = lien.OfferPrice,
            PurchasePrice = lien.PurchasePrice,
            PayoffAmount = lien.PayoffAmount,
            SubjectFirstName = lien.SubjectFirstName,
            SubjectLastName = lien.SubjectLastName,
            Jurisdiction = lien.Jurisdiction,
            IncidentDate = lien.IncidentDate,
            Description = lien.Description,
            CreatedByUserId = createdByUserId,
            UpdatedByUserId = createdByUserId,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };
    }
}
