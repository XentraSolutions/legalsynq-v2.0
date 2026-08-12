using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Xenia.Domain.Email;

namespace Xenia.Infrastructure.Persistence.Configurations;

internal sealed class EmailOperationalAlertConfiguration : IEntityTypeConfiguration<EmailOperationalAlert>
{

    public void Configure(EntityTypeBuilder<EmailOperationalAlert> builder)
    {
        builder.ToTable("xn_email_operational_alerts");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasColumnName("id").HasColumnType("char(36)").ValueGeneratedNever();

        builder.Property(e => e.TenantId)
            .HasColumnName("tenant_id").HasColumnType("char(36)").IsRequired();

        builder.Property(e => e.EmailSourceId)
            .HasColumnName("email_source_id").HasColumnType("char(36)");

        builder.Property(e => e.ProviderType)
            .HasColumnName("provider_type")
            ;

        builder.Property(e => e.AlertType)
            .HasColumnName("alert_type")
            .IsRequired();

        builder.Property(e => e.Severity)
            .HasColumnName("severity")
            .IsRequired();

        builder.Property(e => e.Status)
            .HasColumnName("status")
            .IsRequired();

        builder.Property(e => e.DeduplicationKey)
            .HasColumnName("deduplication_key")
            .HasMaxLength(EmailOperationalAlert.DeduplicationKeyMaxLength).IsRequired();

        builder.Property(e => e.Title)
            .HasColumnName("title")
            .HasMaxLength(EmailOperationalAlert.TitleMaxLength).IsRequired();

        builder.Property(e => e.SafeDescription)
            .HasColumnName("safe_description")
            .HasMaxLength(EmailOperationalAlert.SafeDescriptionMaxLength).IsRequired();

        builder.Property(e => e.FirstObservedAt)
            .HasColumnName("first_observed_at").HasColumnType("datetime(6)").IsRequired();

        builder.Property(e => e.LastObservedAt)
            .HasColumnName("last_observed_at").HasColumnType("datetime(6)").IsRequired();

        builder.Property(e => e.OccurrenceCount)
            .HasColumnName("occurrence_count").IsRequired();

        builder.Property(e => e.AcknowledgedAt)
            .HasColumnName("acknowledged_at").HasColumnType("datetime(6)");

        builder.Property(e => e.AcknowledgedBy)
            .HasColumnName("acknowledged_by").HasColumnType("char(36)");

        builder.Property(e => e.ResolvedAt)
            .HasColumnName("resolved_at").HasColumnType("datetime(6)");

        builder.Property(e => e.ResolvedBy)
            .HasColumnName("resolved_by").HasColumnType("char(36)");

        builder.Property(e => e.ResolutionReason)
            .HasColumnName("resolution_reason")
            .HasMaxLength(EmailOperationalAlert.ResolutionReasonMaxLength);

        builder.Property(e => e.SuppressedUntil)
            .HasColumnName("suppressed_until").HasColumnType("datetime(6)");

        builder.Property(e => e.CorrelationId)
            .HasColumnName("correlation_id")
            .HasMaxLength(EmailOperationalAlert.CorrelationIdMaxLength);

        builder.Property(e => e.CreatedAt)
            .HasColumnName("created_at").HasColumnType("datetime(6)").IsRequired();

        builder.Property(e => e.UpdatedAt)
            .HasColumnName("updated_at").HasColumnType("datetime(6)").IsRequired();

        builder.Property(e => e.Version)
            .HasColumnName("version").IsRequired().HasDefaultValue(1);

        // Indexes
        builder.HasIndex(e => e.TenantId)
            .HasDatabaseName("ix_op_alerts_tenant");

        builder.HasIndex(e => new { e.TenantId, e.EmailSourceId })
            .HasDatabaseName("ix_op_alerts_source");

        builder.HasIndex(e => new { e.TenantId, e.AlertType })
            .HasDatabaseName("ix_op_alerts_type");

        builder.HasIndex(e => new { e.TenantId, e.Severity })
            .HasDatabaseName("ix_op_alerts_severity");

        builder.HasIndex(e => new { e.TenantId, e.Status })
            .HasDatabaseName("ix_op_alerts_status");

        builder.HasIndex(e => e.FirstObservedAt)
            .HasDatabaseName("ix_op_alerts_first_observed");

        builder.HasIndex(e => e.LastObservedAt)
            .HasDatabaseName("ix_op_alerts_last_observed");

        // Deduplication: unique open alert per (TenantId, DeduplicationKey, Status=Open)
        // Enforced at application level (cannot use partial index in MySQL via EF easily)
        builder.HasIndex(e => new { e.TenantId, e.DeduplicationKey })
            .HasDatabaseName("ix_op_alerts_dedup_key");
    }
}
