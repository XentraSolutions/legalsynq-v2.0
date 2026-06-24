using BuildingBlocks.Domain;

namespace Liens.Domain.Entities;

public class SellingPortfolioBuyer : AuditableEntity
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid PortfolioId { get; private set; }
    public Guid BuyerOrgId { get; private set; }

    private SellingPortfolioBuyer() { }

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
