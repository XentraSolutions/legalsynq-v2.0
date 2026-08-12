using BuildingBlocks.Domain;

namespace Liens.Domain.Entities;

public class SellingPortfolioBuyer : AuditableEntity
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid PortfolioId { get; private set; }
    public Guid BuyerOrgId { get; private set; }
    public Guid? BuyerCompanyId { get; private set; }

    private SellingPortfolioBuyer() { }

    public void LinkCanonicalBuyer(Guid? companyId)
    {
        if (companyId == Guid.Empty) throw new ArgumentException("Canonical buyer company id cannot be empty.", nameof(companyId));
        BuyerCompanyId = companyId;
    }

    public void ReassignCanonicalBuyerCompany(
        Guid sourceCompanyId, Guid targetCompanyId, Guid updatedByUserId)
    {
        if (sourceCompanyId == Guid.Empty) throw new ArgumentException("Source company id is required.", nameof(sourceCompanyId));
        if (targetCompanyId == Guid.Empty) throw new ArgumentException("Target company id is required.", nameof(targetCompanyId));
        if (sourceCompanyId == targetCompanyId) throw new ArgumentException("Source and target company ids must differ.", nameof(targetCompanyId));
        if (updatedByUserId == Guid.Empty) throw new ArgumentException("UpdatedByUserId is required.", nameof(updatedByUserId));
        if (BuyerCompanyId != sourceCompanyId) return;
        BuyerCompanyId = targetCompanyId;
        UpdatedByUserId = updatedByUserId;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public static SellingPortfolioBuyer Create(
        Guid tenantId,
        Guid portfolioId,
        Guid buyerOrgId,
        Guid createdByUserId)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("TenantId is required.", nameof(tenantId));
        if (portfolioId == Guid.Empty) throw new ArgumentException("PortfolioId is required.", nameof(portfolioId));
        if (buyerOrgId == Guid.Empty) throw new ArgumentException("BuyerOrgId is required.", nameof(buyerOrgId));
        if (createdByUserId == Guid.Empty) throw new ArgumentException("CreatedByUserId is required.", nameof(createdByUserId));

        var now = DateTime.UtcNow;
        return new SellingPortfolioBuyer
        {
            Id = Guid.CreateVersion7(),
            TenantId = tenantId,
            PortfolioId = portfolioId,
            BuyerOrgId = buyerOrgId,
            CreatedByUserId = createdByUserId,
            UpdatedByUserId = createdByUserId,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };
    }
}
