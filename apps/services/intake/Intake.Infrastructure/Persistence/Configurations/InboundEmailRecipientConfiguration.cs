using Intake.Domain.Emails;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Intake.Infrastructure.Persistence.Configurations;

public sealed class InboundEmailRecipientConfiguration
    : IEntityTypeConfiguration<InboundEmailRecipient>
{
    public void Configure(EntityTypeBuilder<InboundEmailRecipient> builder)
    {
        builder.ToTable("InboundEmailRecipients");
        builder.HasKey(recipient => recipient.Id);

        builder.Property(recipient => recipient.Id).HasColumnType("char(36)");
        builder.Property(recipient => recipient.InboundEmailId).HasColumnType("char(36)").IsRequired();
        builder.Property(recipient => recipient.RecipientType).HasMaxLength(8).IsRequired();
        builder.Property(recipient => recipient.EmailAddress).HasMaxLength(320).IsRequired();
        builder.Property(recipient => recipient.NormalizedEmailAddress)
            .HasMaxLength(320)
            .UseCollation("utf8mb4_bin")
            .IsRequired();
        builder.Property(recipient => recipient.DisplayName).HasMaxLength(512);
        builder.Property(recipient => recipient.Ordinal).IsRequired();

        builder.HasOne(recipient => recipient.InboundEmail)
            .WithMany(email => email.Recipients)
            .HasForeignKey(recipient => recipient.InboundEmailId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(recipient => new { recipient.InboundEmailId, recipient.RecipientType });
    }
}