using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Xenia.Domain.Email;

namespace Xenia.Infrastructure.Persistence.Configurations;

internal sealed class EmailOperationalSettingsConfiguration : IEntityTypeConfiguration<EmailOperationalSettings>
{
    public void Configure(EntityTypeBuilder<EmailOperationalSettings> builder)
    {
        builder.ToTable("xn_email_operational_settings");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasColumnName("id").HasColumnType("char(36)").ValueGeneratedNever();

        builder.Property(e => e.TenantId)
            .HasColumnName("tenant_id").HasColumnType("char(36)").IsRequired();

        builder.Property(e => e.DefaultDashboardRangeDays)
            .HasColumnName("default_dashboard_range_days").IsRequired();

        builder.Property(e => e.SourceFailureAlertThreshold)
            .HasColumnName("source_failure_alert_threshold").IsRequired();

        builder.Property(e => e.StaleSyncThresholdMinutes)
            .HasColumnName("stale_sync_threshold_minutes").IsRequired();

        builder.Property(e => e.LockWarningThresholdMinutes)
            .HasColumnName("lock_warning_threshold_minutes").IsRequired();

        builder.Property(e => e.MaximumRetryCount)
            .HasColumnName("maximum_retry_count").IsRequired();

        builder.Property(e => e.CancellationTimeoutSeconds)
            .HasColumnName("cancellation_timeout_seconds").IsRequired();

        builder.Property(e => e.MetricsEnabled)
            .HasColumnName("metrics_enabled").IsRequired();

        builder.Property(e => e.NotificationAlertsEnabled)
            .HasColumnName("notification_alerts_enabled").IsRequired();

        builder.Property(e => e.DefaultRunPageSize)
            .HasColumnName("default_run_page_size").IsRequired();

        builder.Property(e => e.DefaultMessagePageSize)
            .HasColumnName("default_message_page_size").IsRequired();

        builder.Property(e => e.OperationalPollingIntervalSeconds)
            .HasColumnName("operational_polling_interval_seconds").IsRequired();

        builder.Property(e => e.MessageMetadataRetentionDays)
            .HasColumnName("message_metadata_retention_days").IsRequired();

        builder.Property(e => e.MessageBodyRetentionDays)
            .HasColumnName("message_body_retention_days").IsRequired();

        builder.Property(e => e.ValidationHistoryRetentionDays)
            .HasColumnName("validation_history_retention_days").IsRequired();

        builder.Property(e => e.IngestionRunRetentionDays)
            .HasColumnName("ingestion_run_retention_days").IsRequired();

        builder.Property(e => e.AlertRetentionDays)
            .HasColumnName("alert_retention_days").IsRequired();

        builder.Property(e => e.AttachmentReferenceRetentionDays)
            .HasColumnName("attachment_reference_retention_days").IsRequired();

        builder.Property(e => e.PurgeBatchSize)
            .HasColumnName("purge_batch_size").IsRequired();

        builder.Property(e => e.RetentionDryRunDefault)
            .HasColumnName("retention_dry_run_default").IsRequired();

        builder.Property(e => e.LegalHoldEnabled)
            .HasColumnName("legal_hold_enabled").IsRequired();

        builder.Property(e => e.RetentionEnabled)
            .HasColumnName("retention_enabled").IsRequired();

        builder.Property(e => e.CreatedAt)
            .HasColumnName("created_at").HasColumnType("datetime(6)").IsRequired();

        builder.Property(e => e.UpdatedAt)
            .HasColumnName("updated_at").HasColumnType("datetime(6)").IsRequired();

        builder.Property(e => e.UpdatedBy)
            .HasColumnName("updated_by")
            .HasMaxLength(EmailOperationalSettings.UpdatedByMaxLength);

        builder.Property(e => e.Version)
            .HasColumnName("version").IsRequired().HasDefaultValue(1);

        // Unique per tenant
        builder.HasIndex(e => e.TenantId)
            .IsUnique()
            .HasDatabaseName("ux_op_settings_tenant");
    }
}
