using CareConnect.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CareConnect.Infrastructure.Data.Configurations;

public class ReferralAttachmentConfiguration : IEntityTypeConfiguration<ReferralAttachment>
{
    public void Configure(EntityTypeBuilder<ReferralAttachment> builder)
    {
        builder.ToTable("cc_ReferralAttachments");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.Id).IsRequired();
        builder.Property(a => a.TenantId).IsRequired();
        builder.Property(a => a.ReferralId).IsRequired();
        builder.Property(a => a.ReferralCommentId);
        builder.Property(a => a.FileName).IsRequired().HasMaxLength(500);
        builder.Property(a => a.ContentType).IsRequired().HasMaxLength(200);
        builder.Property(a => a.FileSizeBytes).IsRequired();
        builder.Property(a => a.ExternalDocumentId).HasMaxLength(500);
        builder.Property(a => a.ExternalStorageProvider).HasMaxLength(100);
        builder.Property(a => a.Status).IsRequired().HasMaxLength(20);
        builder.Property(a => a.Notes).HasMaxLength(1000);
        builder.Property(a => a.CreatedAtUtc).IsRequired();
        builder.Property(a => a.UpdatedAtUtc).IsRequired();
        builder.Property(a => a.CreatedByUserId);
        builder.Property(a => a.UpdatedByUserId);

        builder.HasIndex(a => new { a.TenantId, a.ReferralId, a.CreatedAtUtc });
        builder.HasIndex(a => new { a.TenantId, a.Status });
        builder.HasIndex(a => a.ReferralCommentId)
            .HasDatabaseName("IX_cc_ReferralAttachments_ReferralCommentId");
        builder.HasIndex(a => new { a.TenantId, a.ReferralId, a.ReferralCommentId, a.CreatedAtUtc })
            .HasDatabaseName("IX_cc_ReferralAttachments_ReferralComment");

        builder.HasOne(a => a.Referral)
               .WithMany()
               .HasForeignKey(a => a.ReferralId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(a => a.ReferralComment)
               .WithMany(c => c.Attachments)
               .HasForeignKey(a => a.ReferralCommentId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}
