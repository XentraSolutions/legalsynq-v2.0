using Liens.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Liens.Infrastructure.Persistence.Configurations;

public class SellingPortfolioLienConfiguration : IEntityTypeConfiguration<SellingPortfolioLien>
{
    public void Configure(EntityTypeBuilder<SellingPortfolioLien> builder)
    {
        builder.ToTable("liens_SellingPortfolioLiens");

        builder.HasKey(l => l.Id);

        builder.Property(l => l.Id).IsRequired();
        builder.Property(l => l.TenantId).IsRequired();
        builder.Property(l => l.PortfolioId).IsRequired();
        builder.Property(l => l.LienId).IsRequired();

        builder.Property(l => l.LienNumber)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(l => l.LienExternalId)
            .HasMaxLength(200);

        builder.Property(l => l.CaseExternalId)
            .HasMaxLength(200);

        builder.Property(l => l.LienType)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(l => l.LienLifecycleStatus)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(l => l.OriginalAmount)
            .IsRequired()
            .HasColumnType("decimal(18,2)");

        builder.Property(l => l.CurrentBalance)
            .HasColumnType("decimal(18,2)");

        builder.Property(l => l.OfferPrice)
            .HasColumnType("decimal(18,2)");

        builder.Property(l => l.PurchasePrice)
            .HasColumnType("decimal(18,2)");

        builder.Property(l => l.PayoffAmount)
            .HasColumnType("decimal(18,2)");

        builder.Property(l => l.SubjectFirstName)
            .HasMaxLength(100);

        builder.Property(l => l.SubjectLastName)
            .HasMaxLength(100);

        builder.Property(l => l.Jurisdiction)
            .HasMaxLength(100);

        builder.Property(l => l.IncidentDate)
            .HasColumnType("date");

        builder.Property(l => l.Description)
            .HasMaxLength(4000);

        builder.Property(l => l.CreatedByUserId).IsRequired();
        builder.Property(l => l.UpdatedByUserId);
        builder.Property(l => l.CreatedAtUtc).IsRequired();
        builder.Property(l => l.UpdatedAtUtc).IsRequired();

        builder.HasIndex(l => new { l.TenantId, l.PortfolioId, l.LienId })
            .IsUnique()
            .HasDatabaseName("UX_SellingPortfolioLiens_TenantId_PortfolioId_LienId");

        builder.HasIndex(l => new { l.TenantId, l.LienId })
            .HasDatabaseName("IX_SellingPortfolioLiens_TenantId_LienId");

        builder.HasIndex(l => new { l.TenantId, l.CaseId })
            .HasDatabaseName("IX_SellingPortfolioLiens_TenantId_CaseId");

        builder.HasOne<Lien>()
            .WithMany()
            .HasForeignKey(l => l.LienId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
