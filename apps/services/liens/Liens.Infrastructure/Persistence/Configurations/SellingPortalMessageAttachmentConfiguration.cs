using Liens.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Liens.Infrastructure.Persistence.Configurations;

public sealed class SellingPortalMessageAttachmentConfiguration : IEntityTypeConfiguration<SellingPortalMessageAttachment>
{
    public void Configure(EntityTypeBuilder<SellingPortalMessageAttachment> builder)
    {
        builder.ToTable("liens_SellingPortalMessageAttachments");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.Id).IsRequired();
        builder.Property(a => a.TenantId).IsRequired();
        builder.Property(a => a.LienId).IsRequired();
        builder.Property(a => a.SellerOrgId).IsRequired();
        builder.Property(a => a.BuyerOrgId).IsRequired();
        builder.Property(a => a.BuyerContactId).IsRequired();
        builder.Property(a => a.AccessLinkId).IsRequired();
        builder.Property(a => a.MessageId).IsRequired();
        builder.Property(a => a.DocumentId).IsRequired();
        builder.Property(a => a.FileName).IsRequired().HasMaxLength(255);
        builder.Property(a => a.ContentType).IsRequired().HasMaxLength(160);
        builder.Property(a => a.FileSizeBytes).IsRequired();
        builder.Property(a => a.CreatedByUserId).IsRequired();
        builder.Property(a => a.UpdatedByUserId);
        builder.Property(a => a.CreatedAtUtc).IsRequired();
        builder.Property(a => a.UpdatedAtUtc).IsRequired();

        builder.HasIndex(a => new { a.TenantId, a.MessageId, a.CreatedAtUtc })
            .HasDatabaseName("IX_SellingPortalMessageAttachments_Tenant_Message_Created");

        builder.HasIndex(a => new { a.TenantId, a.LienId, a.SellerOrgId, a.BuyerOrgId, a.BuyerContactId })
            .HasDatabaseName("IX_SellingPortalMessageAttachments_Tenant_Lien_Participants");

        builder.HasIndex(a => new { a.TenantId, a.DocumentId })
            .HasDatabaseName("IX_SellingPortalMessageAttachments_Tenant_Document");

        builder.HasOne<SellingPortalMessage>()
            .WithMany()
            .HasForeignKey(a => a.MessageId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Lien>()
            .WithMany()
            .HasForeignKey(a => a.LienId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<SellingBuyerAccessLink>()
            .WithMany()
            .HasForeignKey(a => a.AccessLinkId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
