using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Notifications.Domain;

namespace Notifications.Infrastructure.Data.Configurations;

public class NotificationEventConfiguration : IEntityTypeConfiguration<NotificationEvent>
{
    public void Configure(EntityTypeBuilder<NotificationEvent> builder)
    {
        builder.ToTable("ntf_notification_events");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.TenantId).HasColumnName("tenant_id");
        builder.Property(e => e.NotificationId).HasColumnName("notification_id");
        builder.Property(e => e.NotificationAttemptId).HasColumnName("notification_attempt_id");
        builder.Property(e => e.Provider).HasColumnName("provider").HasMaxLength(50);
        builder.Property(e => e.Channel).HasColumnName("channel").HasMaxLength(20);
        builder.Property(e => e.RawEventType).HasColumnName("raw_event_type").HasMaxLength(100);
        builder.Property(e => e.NormalizedEventType).HasColumnName("normalized_event_type").HasMaxLength(50);
        builder.Property(e => e.EventTimestamp).HasColumnName("event_timestamp");
        builder.Property(e => e.ProviderMessageId).HasColumnName("provider_message_id").HasMaxLength(500);
        builder.Property(e => e.MetadataJson).HasColumnName("metadata_json").HasColumnType("text");
        builder.Property(e => e.DedupKey).HasColumnName("dedup_key").HasMaxLength(500);
        builder.Property(e => e.CreatedAt).HasColumnName("created_at");
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at");

        builder.HasIndex(e => e.DedupKey).HasDatabaseName("idx_events_dedup_key").IsUnique();
        builder.HasIndex(e => e.NotificationId).HasDatabaseName("idx_events_notification_id");
    }
}

public class RecipientContactHealthConfiguration : IEntityTypeConfiguration<RecipientContactHealth>
{
    public void Configure(EntityTypeBuilder<RecipientContactHealth> builder)
    {
        builder.ToTable("ntf_recipient_contact_health");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.TenantId).HasColumnName("tenant_id");
        builder.Property(e => e.Channel).HasColumnName("channel").HasMaxLength(20);
        builder.Property(e => e.ContactValue).HasColumnName("contact_value").HasMaxLength(500);
        builder.Property(e => e.HealthStatus).HasColumnName("health_status").HasMaxLength(30).HasDefaultValue("valid");
        builder.Property(e => e.BounceCount).HasColumnName("bounce_count").HasDefaultValue(0);
        builder.Property(e => e.ComplaintCount).HasColumnName("complaint_count").HasDefaultValue(0);
        builder.Property(e => e.DeliveryCount).HasColumnName("delivery_count").HasDefaultValue(0);
        builder.Property(e => e.LastBounceAt).HasColumnName("last_bounce_at");
        builder.Property(e => e.LastComplaintAt).HasColumnName("last_complaint_at");
        builder.Property(e => e.LastDeliveryAt).HasColumnName("last_delivery_at");
        builder.Property(e => e.LastRawEventType).HasColumnName("last_raw_event_type").HasMaxLength(100);
        builder.Property(e => e.CreatedAt).HasColumnName("created_at");
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at");

        builder.HasIndex(e => new { e.TenantId, e.Channel, e.ContactValue })
            .HasDatabaseName("uq_recipient_contact_health")
            .IsUnique();
    }
}

public class DeliveryIssueConfiguration : IEntityTypeConfiguration<DeliveryIssue>
{
    public void Configure(EntityTypeBuilder<DeliveryIssue> builder)
    {
        builder.ToTable("ntf_delivery_issues");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.TenantId).HasColumnName("tenant_id");
        builder.Property(e => e.NotificationId).HasColumnName("notification_id");
        builder.Property(e => e.NotificationAttemptId).HasColumnName("notification_attempt_id");
        builder.Property(e => e.Channel).HasColumnName("channel").HasMaxLength(20);
        builder.Property(e => e.Provider).HasColumnName("provider").HasMaxLength(50);
        builder.Property(e => e.IssueType).HasColumnName("issue_type").HasMaxLength(50);
        builder.Property(e => e.RecommendedAction).HasColumnName("recommended_action").HasColumnType("text");
        builder.Property(e => e.DetailsJson).HasColumnName("details_json").HasColumnType("text");
        builder.Property(e => e.IsResolved).HasColumnName("is_resolved").HasDefaultValue(false);
        builder.Property(e => e.ResolvedAt).HasColumnName("resolved_at");
        builder.Property(e => e.CreatedAt).HasColumnName("created_at");
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at");

        builder.HasIndex(e => new { e.TenantId, e.NotificationId }).HasDatabaseName("idx_delivery_issues_tenant_notification");
    }
}

public class ContactSuppressionConfiguration : IEntityTypeConfiguration<ContactSuppression>
{
    public void Configure(EntityTypeBuilder<ContactSuppression> builder)
    {
        builder.ToTable("ntf_contact_suppressions");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.TenantId).HasColumnName("tenant_id");
        builder.Property(e => e.Channel).HasColumnName("channel").HasMaxLength(20);
        builder.Property(e => e.ContactValue).HasColumnName("contact_value").HasMaxLength(500);
        builder.Property(e => e.SuppressionType).HasColumnName("suppression_type").HasMaxLength(50);
        builder.Property(e => e.Status).HasColumnName("status").HasMaxLength(20).HasDefaultValue("active");
        builder.Property(e => e.Reason).HasColumnName("reason").HasColumnType("text");
        builder.Property(e => e.Source).HasColumnName("source").HasMaxLength(50);
        builder.Property(e => e.ExpiresAt).HasColumnName("expires_at");
        builder.Property(e => e.CreatedBy).HasColumnName("created_by").HasMaxLength(255);
        builder.Property(e => e.Notes).HasColumnName("notes").HasColumnType("text");
        builder.Property(e => e.CreatedAt).HasColumnName("created_at");
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at");

        builder.HasIndex(e => new { e.TenantId, e.Channel, e.ContactValue })
            .HasDatabaseName("idx_suppressions_tenant_channel_contact");
    }
}
