using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Notifications.Domain;

namespace Notifications.Infrastructure.Data.Configurations;

public class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.ToTable("ntf_notifications");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.TenantId).HasColumnName("tenant_id");
        builder.Property(e => e.Channel).HasColumnName("channel").HasMaxLength(20);
        builder.Property(e => e.Status).HasColumnName("status").HasMaxLength(20).HasDefaultValue("accepted");
        builder.Property(e => e.RecipientJson).HasColumnName("recipient_json").HasColumnType("text");
        builder.Property(e => e.MessageJson).HasColumnName("message_json").HasColumnType("text");
        builder.Property(e => e.MetadataJson).HasColumnName("metadata_json").HasColumnType("text");
        builder.Property(e => e.IdempotencyKey).HasColumnName("idempotency_key").HasMaxLength(255);
        builder.Property(e => e.ProviderUsed).HasColumnName("provider_used").HasMaxLength(100);
        builder.Property(e => e.FailureCategory).HasColumnName("failure_category").HasMaxLength(100);
        builder.Property(e => e.LastErrorMessage).HasColumnName("last_error_message").HasColumnType("text");
        builder.Property(e => e.TemplateId).HasColumnName("template_id");
        builder.Property(e => e.TemplateVersionId).HasColumnName("template_version_id");
        builder.Property(e => e.TemplateKey).HasColumnName("template_key").HasMaxLength(200);
        builder.Property(e => e.RenderedSubject).HasColumnName("rendered_subject").HasColumnType("text");
        builder.Property(e => e.RenderedBody).HasColumnName("rendered_body").HasColumnType("text");
        builder.Property(e => e.RenderedText).HasColumnName("rendered_text").HasColumnType("text");
        builder.Property(e => e.ProviderOwnershipMode).HasColumnName("provider_ownership_mode").HasMaxLength(50);
        builder.Property(e => e.ProviderConfigId).HasColumnName("provider_config_id");
        builder.Property(e => e.PlatformFallbackUsed).HasColumnName("platform_fallback_used").HasDefaultValue(false);
        builder.Property(e => e.BlockedByPolicy).HasColumnName("blocked_by_policy").HasDefaultValue(false);
        builder.Property(e => e.BlockedReasonCode).HasColumnName("blocked_reason_code").HasMaxLength(100);
        builder.Property(e => e.OverrideUsed).HasColumnName("override_used").HasDefaultValue(false);
        builder.Property(e => e.CreatedAt).HasColumnName("created_at");
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at");

        builder.HasIndex(e => new { e.TenantId, e.IdempotencyKey })
            .HasDatabaseName("uq_notifications_tenant_idempotency")
            .IsUnique()
            .HasFilter("idempotency_key IS NOT NULL");
    }
}

public class NotificationAttemptConfiguration : IEntityTypeConfiguration<NotificationAttempt>
{
    public void Configure(EntityTypeBuilder<NotificationAttempt> builder)
    {
        builder.ToTable("ntf_notification_attempts");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.TenantId).HasColumnName("tenant_id");
        builder.Property(e => e.NotificationId).HasColumnName("notification_id");
        builder.Property(e => e.Channel).HasColumnName("channel").HasMaxLength(20);
        builder.Property(e => e.Provider).HasColumnName("provider").HasMaxLength(100);
        builder.Property(e => e.Status).HasColumnName("status").HasMaxLength(20).HasDefaultValue("pending");
        builder.Property(e => e.AttemptNumber).HasColumnName("attempt_number").HasDefaultValue(1);
        builder.Property(e => e.ProviderMessageId).HasColumnName("provider_message_id").HasMaxLength(500);
        builder.Property(e => e.ProviderOwnershipMode).HasColumnName("provider_ownership_mode").HasMaxLength(50);
        builder.Property(e => e.ProviderConfigId).HasColumnName("provider_config_id");
        builder.Property(e => e.FailureCategory).HasColumnName("failure_category").HasMaxLength(100);
        builder.Property(e => e.ErrorMessage).HasColumnName("error_message").HasColumnType("text");
        builder.Property(e => e.IsFailover).HasColumnName("is_failover").HasDefaultValue(false);
        builder.Property(e => e.CompletedAt).HasColumnName("completed_at");
        builder.Property(e => e.CreatedAt).HasColumnName("created_at");
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at");

        builder.HasIndex(e => e.NotificationId).HasDatabaseName("idx_attempts_notification_id");
        builder.HasIndex(e => e.ProviderMessageId).HasDatabaseName("idx_attempts_provider_message_id");
    }
}
