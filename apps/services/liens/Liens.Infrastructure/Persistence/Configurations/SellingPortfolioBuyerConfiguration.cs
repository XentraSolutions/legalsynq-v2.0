using Liens.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Liens.Infrastructure.Persistence.Configurations;

public class SellingPortfolioBuyerConfiguration : IEntityTypeConfiguration<SellingPortfolioBuyer>
{
    public void Configure(EntityTypeBuilder<SellingPortfolioBuyer> builder)
    {
        builder.ToTable("liens_SellingPortfolioBuyers");

        builder.HasKey(b => b.Id);

        builder.Property(b => b.Id).IsRequired();
        builder.Property(b => b.TenantId).IsRequired();
        builder.Property(b => b.PortfolioId).IsRequired();
        builder.Property(b => b.BuyerOrgId).IsRequired();
        builder.Property(b => b.BuyerCompanyId);

        builder.Property(b => b.CreatedByUserId).IsRequired();
        builder.Property(b => b.UpdatedByUserId);
        builder.Property(b => b.CreatedAtUtc).IsRequired();
        builder.Property(b => b.UpdatedAtUtc).IsRequired();

        builder.HasIndex(b => new { b.TenantId, b.PortfolioId, b.BuyerOrgId })
            .IsUnique()
            .HasDatabaseName("UX_SellingPortfolioBuyers_TenantId_PortfolioId_BuyerOrgId");

        builder.HasIndex(b => new { b.TenantId, b.BuyerOrgId })
            .HasDatabaseName("IX_SellingPortfolioBuyers_TenantId_BuyerOrgId");

        builder.HasOne<Company>().WithMany().HasForeignKey(b => b.BuyerCompanyId).OnDelete(DeleteBehavior.Restrict);
    }
}
