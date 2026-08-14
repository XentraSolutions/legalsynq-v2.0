using Intake.Domain.Emails;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Intake.Infrastructure.Persistence.Configurations;

public sealed class InboundEmailAttachmentMetadataConfiguration
    : IEntityTypeConfiguration<InboundEmailAttachmentMetadata>
{
    public void Configure(EntityTypeBuilder<InboundEmailAttachmentMetadata> builder)
    {
        builder.ToTable("InboundEmailAttachmentMetadata");
        builder.HasKey(attachment => attachment.Id);

        builder.Property(attachment => attachment.Id).HasColumnType("char(36)");
        builder.Property(attachment => attachment.InboundEmailId).HasColumnType("char(36)").IsRequired();
        builder.Property(attachment => attachment.ProviderAttachmentId).HasMaxLength(512);
        builder.Property(attachment => attachment.FileName).HasMaxLength(1024).IsRequired();
        builder.Property(attachment => attachment.ContentType).HasMaxLength(255);
        builder.Property(attachment => attachment.ContentDisposition).HasMaxLength(255);
        builder.Property(attachment => attachment.ContentId).HasMaxLength(512);
        builder.Property(attachment => attachment.Sha256).HasMaxLength(64);
        builder.Property(attachment => attachment.Ordinal).IsRequired();

        builder.HasOne(attachment => attachment.InboundEmail)
            .WithMany(email => email.AttachmentMetadata)
            .HasForeignKey(attachment => attachment.InboundEmailId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(attachment => new { attachment.InboundEmailId, attachment.Ordinal });
    }
}