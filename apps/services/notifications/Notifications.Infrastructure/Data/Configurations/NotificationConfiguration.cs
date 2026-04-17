using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Notifications.Domain;

namespace Notifications.Infrastructure.Data.Configurations;

public class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.ToTable("ntf_Notifications");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Channel).HasMaxLength(20);
        builder.Property(e => e.Status).HasMaxLength(20).HasDefaultValue("accepted");
        builder.Property(e => e.RecipientJson).HasColumnType("text");
        builder.Property(e => e.MessageJson).HasColumnType("text");
        builder.Property(e => e.MetadataJson).HasColumnType("text");
        builder.Property(e => e.IdempotencyKey).HasMaxLength(255);
        builder.Property(e => e.ProviderUsed).HasMaxLength(100);
        builder.Property(e => e.FailureCategory).HasMaxLength(100);
        builder.Property(e => e.LastErrorMessage).HasColumnType("text");
        builder.Property(e => e.TemplateKey).HasMaxLength(200);
        builder.Property(e => e.RenderedSubject).HasColumnType("text");
        builder.Property(e => e.RenderedBody).HasColumnType("text");
        builder.Property(e => e.RenderedText).HasColumnType("text");
        builder.Property(e => e.ProviderOwnershipMode).HasMaxLength(50);
        builder.Property(e => e.PlatformFallbackUsed).HasDefaultValue(false);
        builder.Property(e => e.BlockedByPolicy).HasDefaultValue(false);
        builder.Property(e => e.BlockedReasonCode).HasMaxLength(100);
        builder.Property(e => e.OverrideUsed).HasDefaultValue(false);
        builder.Property(e => e.Severity).HasMaxLength(50);
        builder.Property(e => e.Category).HasMaxLength(100);

        builder.HasIndex(e => new { e.TenantId, e.IdempotencyKey })
            .HasDatabaseName("UX_Notifications_TenantId_IdempotencyKey")
            .IsUnique()
            .HasFilter("IdempotencyKey IS NOT NULL");
    }
}

public class NotificationAttemptConfiguration : IEntityTypeConfiguration<NotificationAttempt>
{
    public void Configure(EntityTypeBuilder<NotificationAttempt> builder)
    {
        builder.ToTable("ntf_NotificationAttempts");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Channel).HasMaxLength(20);
        builder.Property(e => e.Provider).HasMaxLength(100);
        builder.Property(e => e.Status).HasMaxLength(20).HasDefaultValue("pending");
        builder.Property(e => e.AttemptNumber).HasDefaultValue(1);
        builder.Property(e => e.ProviderMessageId).HasMaxLength(500);
        builder.Property(e => e.ProviderOwnershipMode).HasMaxLength(50);
        builder.Property(e => e.FailureCategory).HasMaxLength(100);
        builder.Property(e => e.ErrorMessage).HasColumnType("text");
        builder.Property(e => e.IsFailover).HasDefaultValue(false);

        builder.HasIndex(e => e.NotificationId).HasDatabaseName("IX_NotificationAttempts_NotificationId");
        builder.HasIndex(e => e.ProviderMessageId).HasDatabaseName("IX_NotificationAttempts_ProviderMessageId");
    }
}
