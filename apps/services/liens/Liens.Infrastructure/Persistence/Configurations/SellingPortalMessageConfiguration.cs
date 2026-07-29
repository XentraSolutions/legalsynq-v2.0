using Liens.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Liens.Infrastructure.Persistence.Configurations;

public class SellingPortalMessageConfiguration : IEntityTypeConfiguration<SellingPortalMessage>
{
    public void Configure(EntityTypeBuilder<SellingPortalMessage> builder)
    {
        builder.ToTable("liens_SellingPortalMessages");

        builder.HasKey(m => m.Id);

        builder.Property(m => m.Id).IsRequired();
        builder.Property(m => m.TenantId).IsRequired();
        builder.Property(m => m.LienId).IsRequired();
        builder.Property(m => m.SellerOrgId).IsRequired();
        builder.Property(m => m.BuyerOrgId).IsRequired();
        builder.Property(m => m.BuyerContactId).IsRequired();
        builder.Property(m => m.AccessLinkId).IsRequired();

        builder.Property(m => m.SenderType)
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(m => m.SenderName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(m => m.SenderEmail)
            .HasMaxLength(320);

        builder.Property(m => m.Message)
            .IsRequired()
            .HasMaxLength(400);

        builder.Property(m => m.CreatedByUserId).IsRequired();
        builder.Property(m => m.UpdatedByUserId);
        builder.Property(m => m.CreatedAtUtc).IsRequired();
        builder.Property(m => m.UpdatedAtUtc).IsRequired();

        builder.HasIndex(m => new { m.TenantId, m.LienId, m.SellerOrgId, m.BuyerOrgId, m.BuyerContactId, m.CreatedAtUtc })
            .HasDatabaseName("IX_SellingPortalMessages_Tenant_Lien_Participants_Created");

        builder.HasIndex(m => new { m.TenantId, m.AccessLinkId, m.CreatedAtUtc })
            .HasDatabaseName("IX_SellingPortalMessages_Tenant_AccessLink_Created");

        builder.HasOne<Lien>()
            .WithMany()
            .HasForeignKey(m => m.LienId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<SellingBuyerAccessLink>()
            .WithMany()
            .HasForeignKey(m => m.AccessLinkId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
