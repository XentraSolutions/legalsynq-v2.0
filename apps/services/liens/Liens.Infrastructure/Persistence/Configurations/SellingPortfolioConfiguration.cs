using Liens.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Liens.Infrastructure.Persistence.Configurations;

public class SellingPortfolioConfiguration : IEntityTypeConfiguration<SellingPortfolio>
{
    public void Configure(EntityTypeBuilder<SellingPortfolio> builder)
    {
        builder.ToTable("liens_SellingPortfolios");

        builder.HasKey(p => p.Id);

        builder.Property(p => p.Id).IsRequired();
        builder.Property(p => p.TenantId).IsRequired();
        builder.Property(p => p.SellerOrgId).IsRequired();

        builder.Property(p => p.PortfolioNumber)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(p => p.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(p => p.Description)
            .HasMaxLength(4000);

        builder.Property(p => p.Status)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(p => p.LienCount).IsRequired();

        builder.Property(p => p.OriginalAmountTotal)
            .IsRequired()
            .HasColumnType("decimal(18,2)");

        builder.Property(p => p.CurrentBalanceTotal)
            .HasColumnType("decimal(18,2)");

        builder.Property(p => p.OfferPriceTotal)
            .HasColumnType("decimal(18,2)");

        builder.Property(p => p.PublishedAtUtc);
        builder.Property(p => p.ClosedAtUtc);

        builder.Property(p => p.CreatedByUserId).IsRequired();
        builder.Property(p => p.UpdatedByUserId);
        builder.Property(p => p.CreatedAtUtc).IsRequired();
        builder.Property(p => p.UpdatedAtUtc).IsRequired();

        builder.HasIndex(p => new { p.TenantId, p.PortfolioNumber })
            .IsUnique()
            .HasDatabaseName("UX_SellingPortfolios_TenantId_PortfolioNumber");

        builder.HasIndex(p => new { p.TenantId, p.SellerOrgId, p.Status })
            .HasDatabaseName("IX_SellingPortfolios_TenantId_SellerOrgId_Status");

        builder.HasIndex(p => new { p.TenantId, p.Status })
            .HasDatabaseName("IX_SellingPortfolios_TenantId_Status");

        builder.HasMany(p => p.Liens)
            .WithOne()
            .HasForeignKey(l => l.PortfolioId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(p => p.Buyers)
            .WithOne()
            .HasForeignKey(b => b.PortfolioId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(p => p.StatusHistory)
            .WithOne()
            .HasForeignKey(h => h.PortfolioId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
