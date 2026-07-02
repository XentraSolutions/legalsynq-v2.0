using Liens.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Liens.Infrastructure.Persistence.Configurations;

public class SellingPortfolioActivityConfiguration : IEntityTypeConfiguration<SellingPortfolioActivity>
{
    public void Configure(EntityTypeBuilder<SellingPortfolioActivity> builder)
    {
        builder.ToTable("liens_SellingPortfolioActivities");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.Id).IsRequired();
        builder.Property(a => a.TenantId).IsRequired();
        builder.Property(a => a.PortfolioId).IsRequired();

        builder.Property(a => a.Action)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(a => a.EntityType)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(a => a.EntityId)
            .HasMaxLength(100);

        builder.Property(a => a.ActorUserId).IsRequired();
        builder.Property(a => a.OccurredAtUtc).IsRequired();

        builder.Property(a => a.Summary)
            .IsRequired()
            .HasMaxLength(1000);

        builder.Property(a => a.MetadataJson)
            .HasColumnType("json");

        builder.Property(a => a.CreatedByUserId).IsRequired();
        builder.Property(a => a.UpdatedByUserId);
        builder.Property(a => a.CreatedAtUtc).IsRequired();
        builder.Property(a => a.UpdatedAtUtc).IsRequired();

        builder.HasIndex(a => new { a.TenantId, a.PortfolioId, a.OccurredAtUtc })
            .HasDatabaseName("IX_SellingPortfolioActivities_TenantId_PortfolioId_OccurredAtUtc");

        builder.HasOne<SellingPortfolio>()
            .WithMany()
            .HasForeignKey(a => a.PortfolioId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
