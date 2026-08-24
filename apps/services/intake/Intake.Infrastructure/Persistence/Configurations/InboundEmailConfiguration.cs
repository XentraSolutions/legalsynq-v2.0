using Intake.Domain.Emails;
using Intake.Domain.Sources;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Intake.Infrastructure.Persistence.Configurations;

public sealed class InboundEmailConfiguration : IEntityTypeConfiguration<InboundEmail>
{
    public void Configure(EntityTypeBuilder<InboundEmail> builder)
    {
        builder.ToTable("InboundEmails");
        builder.HasKey(email => email.Id);

        builder.Property(email => email.Id).HasColumnType("char(36)");
        builder.Property(email => email.TenantId).HasColumnType("char(36)").IsRequired();
        builder.Property(email => email.OrgId).HasColumnType("char(36)");
        builder.Property(email => email.TenantIntakeSourceId).HasColumnType("char(36)").IsRequired();
        builder.Property(email => email.Purpose).HasMaxLength(64).IsRequired();
        builder.Property(email => email.ProcessingProfileCode).HasMaxLength(64).IsRequired();
        builder.Property(email => email.Provider).HasMaxLength(64).IsRequired();
        builder.Property(email => email.ProviderMessageId).HasMaxLength(768);
        builder.Property(email => email.ProviderThreadId).HasMaxLength(768);
        builder.Property(email => email.InternetMessageId).HasMaxLength(768);
        builder.Property(email => email.InReplyToMessageId).HasMaxLength(768);
        builder.Property(email => email.ReferencesJson).HasColumnType("longtext").IsRequired();
        builder.Property(email => email.ReceivedAt).HasPrecision(6).IsRequired();
        builder.Property(email => email.ProviderCreatedAt).HasPrecision(6);
        builder.Property(email => email.CapturedAt).HasPrecision(6).IsRequired();
        builder.Property(email => email.FromAddress).HasMaxLength(320);
        builder.Property(email => email.FromDisplayName).HasMaxLength(512);
        builder.Property(email => email.SenderAddress).HasMaxLength(320);
        builder.Property(email => email.SenderDisplayName).HasMaxLength(512);
        builder.Property(email => email.ReplyToAddress).HasMaxLength(320);
        builder.Property(email => email.ReplyToDisplayName).HasMaxLength(512);
        builder.Property(email => email.Subject).HasMaxLength(998).IsRequired();
        builder.Property(email => email.TextBody).HasColumnType("longtext");
        builder.Property(email => email.HtmlBody).HasColumnType("longtext");
        builder.Property(email => email.HeadersJson).HasColumnType("longtext").IsRequired();
        builder.Property(email => email.RawMessageContent).HasColumnType("longtext");
        builder.Property(email => email.RawMessageHash).HasMaxLength(64);
        builder.Property(email => email.CaptureStatus).HasMaxLength(32).IsRequired();
        builder.Property(email => email.ProcessingStatus).HasMaxLength(32).IsRequired();
        builder.Property(email => email.IdempotencyKey).HasMaxLength(256).IsRequired();
        builder.Property(email => email.CreatedAt).HasPrecision(6).IsRequired();
        builder.Property(email => email.UpdatedAt).HasPrecision(6).IsRequired();

        builder.HasOne<TenantIntakeSource>()
            .WithMany()
            .HasForeignKey(email => email.TenantIntakeSourceId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(email => email.IdempotencyKey).IsUnique();
        builder.HasIndex(email => new { email.TenantId, email.ReceivedAt });
        builder.HasIndex(email => new
        {
            email.TenantId,
            email.TenantIntakeSourceId,
            email.ReceivedAt,
        });
        builder.HasIndex(email => new { email.TenantId, email.Provider, email.ReceivedAt });
        builder.HasIndex(email => new { email.TenantId, email.CaptureStatus, email.ReceivedAt });
        builder.HasIndex(email => new { email.TenantId, email.Purpose, email.ReceivedAt });
    }
}