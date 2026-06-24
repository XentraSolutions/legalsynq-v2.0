using Liens.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Liens.Infrastructure.Persistence.Configurations;

public class SellingPortfolioStatusHistoryConfiguration : IEntityTypeConfiguration<SellingPortfolioStatusHistory>
{
    public void Configure(EntityTypeBuilder<SellingPortfolioStatusHistory> builder)
    {
        builder.ToTable("liens_SellingPortfolioStatusHistory");

        builder.HasKey(h => h.Id);

        builder.Property(h => h.Id).IsRequired();
        builder.Property(h => h.TenantId).IsRequired();
        builder.Property(h => h.PortfolioId).IsRequired();

        builder.Property(h => h.FromStatus)
            .HasMaxLength(50);

        builder.Property(h => h.ToStatus)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(h => h.ChangedByUserId).IsRequired();
        builder.Property(h => h.ChangedAtUtc).IsRequired();

        builder.Property(h => h.Notes)
            .HasMaxLength(1000);

        builder.Property(h => h.CreatedByUserId).IsRequired();
        builder.Property(h => h.UpdatedByUserId);
        builder.Property(h => h.CreatedAtUtc).IsRequired();
        builder.Property(h => h.UpdatedAtUtc).IsRequired();

        builder.HasIndex(h => new { h.TenantId, h.PortfolioId, h.ChangedAtUtc })
            .HasDatabaseName("IX_SellingPortfolioStatusHistory_TenantId_PortfolioId_ChangedAtUtc");
    }
}
