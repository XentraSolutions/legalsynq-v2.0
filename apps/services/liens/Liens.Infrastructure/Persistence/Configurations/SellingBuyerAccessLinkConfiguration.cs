using Liens.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Liens.Infrastructure.Persistence.Configurations;

public class SellingBuyerAccessLinkConfiguration : IEntityTypeConfiguration<SellingBuyerAccessLink>
{
    public void Configure(EntityTypeBuilder<SellingBuyerAccessLink> builder)
    {
        builder.ToTable("liens_SellingBuyerAccessLinks");

        builder.HasKey(l => l.Id);

        builder.Property(l => l.Id).IsRequired();
        builder.Property(l => l.TenantId).IsRequired();
        builder.Property(l => l.LienId).IsRequired();
        builder.Property(l => l.SellerOrgId).IsRequired();
        builder.Property(l => l.BuyerOrgId).IsRequired();
        builder.Property(l => l.BuyerContactId).IsRequired();

        builder.Property(l => l.Token)
            .IsRequired()
            .HasMaxLength(128);

        builder.Property(l => l.Purpose)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(l => l.IdempotencyKey)
            .IsRequired()
            .HasMaxLength(280);

        builder.Property(l => l.ExpiresAtUtc).IsRequired();
        builder.Property(l => l.LastAccessedAtUtc);
        builder.Property(l => l.RevokedAtUtc);
        builder.Property(l => l.NotificationId);

        builder.Property(l => l.NotificationStatus)
            .HasMaxLength(50);

        builder.Property(l => l.NotificationSubmittedAtUtc);

        builder.Property(l => l.ResponseStatus)
            .HasMaxLength(50);

        builder.Property(l => l.ResponseAmount)
            .HasColumnType("decimal(18,2)");

        builder.Property(l => l.ResponseNotes)
            .HasMaxLength(4000);

        builder.Property(l => l.RespondedAtUtc);

        builder.Property(l => l.ResponseIdempotencyKey)
            .HasMaxLength(280);

        builder.Property(l => l.CreatedByUserId).IsRequired();
        builder.Property(l => l.UpdatedByUserId);
        builder.Property(l => l.CreatedAtUtc).IsRequired();
        builder.Property(l => l.UpdatedAtUtc).IsRequired();

        builder.HasIndex(l => new { l.TenantId, l.Token })
            .IsUnique()
            .HasDatabaseName("UX_SellingBuyerAccessLinks_TenantId_Token");

        builder.HasIndex(l => new { l.TenantId, l.IdempotencyKey })
            .IsUnique()
            .HasDatabaseName("UX_SellingBuyerAccessLinks_TenantId_IdempotencyKey");

        builder.HasIndex(l => new { l.TenantId, l.LienId, l.BuyerContactId })
            .HasDatabaseName("IX_SellingBuyerAccessLinks_Tenant_Lien_BuyerContact");

        builder.HasOne<Lien>()
            .WithMany()
            .HasForeignKey(l => l.LienId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
