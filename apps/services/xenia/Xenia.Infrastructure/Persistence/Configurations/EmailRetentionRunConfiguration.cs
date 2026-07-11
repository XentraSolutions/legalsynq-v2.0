using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Xenia.Domain.Email;

namespace Xenia.Infrastructure.Persistence.Configurations;

internal sealed class EmailRetentionRunConfiguration : IEntityTypeConfiguration<EmailRetentionRun>
{
    private static readonly EnumToStringConverter<EmailRetentionMode>      _modeConverter   = new();
    private static readonly EnumToStringConverter<EmailRetentionRunStatus> _statusConverter = new();

    public void Configure(EntityTypeBuilder<EmailRetentionRun> builder)
    {
        builder.ToTable("xn_email_retention_runs");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasColumnName("id").HasColumnType("char(36)").ValueGeneratedNever();

        builder.Property(e => e.TenantId)
            .HasColumnName("tenant_id").HasColumnType("char(36)").IsRequired();

        builder.Property(e => e.Mode)
            .HasColumnName("mode")
            .HasConversion(_modeConverter).HasMaxLength(32).IsRequired();

        builder.Property(e => e.Status)
            .HasColumnName("status")
            .HasConversion(_statusConverter).HasMaxLength(32).IsRequired();

        builder.Property(e => e.StartedAt)
            .HasColumnName("started_at").HasColumnType("datetime(6)").IsRequired();

        builder.Property(e => e.CompletedAt)
            .HasColumnName("completed_at").HasColumnType("datetime(6)");

        builder.Property(e => e.MessagesEligible)
            .HasColumnName("messages_eligible").IsRequired();

        builder.Property(e => e.MessagesDeleted)
            .HasColumnName("messages_deleted").IsRequired();

        builder.Property(e => e.BodiesCleared)
            .HasColumnName("bodies_cleared").IsRequired();

        builder.Property(e => e.RunsDeleted)
            .HasColumnName("runs_deleted").IsRequired();

        builder.Property(e => e.AlertsDeleted)
            .HasColumnName("alerts_deleted").IsRequired();

        builder.Property(e => e.AttachmentReferencesDeleted)
            .HasColumnName("attachment_references_deleted").IsRequired();

        builder.Property(e => e.Failures)
            .HasColumnName("failures").IsRequired();

        builder.Property(e => e.SafeErrorSummary)
            .HasColumnName("safe_error_summary")
            .HasMaxLength(EmailRetentionRun.SafeErrorSummaryMaxLength);

        builder.Property(e => e.CorrelationId)
            .HasColumnName("correlation_id")
            .HasMaxLength(EmailRetentionRun.CorrelationIdMaxLength);

        builder.Property(e => e.ActorId)
            .HasColumnName("actor_id").HasColumnType("char(36)");

        builder.Property(e => e.CreatedAt)
            .HasColumnName("created_at").HasColumnType("datetime(6)").IsRequired();

        builder.Property(e => e.UpdatedAt)
            .HasColumnName("updated_at").HasColumnType("datetime(6)").IsRequired();

        builder.HasIndex(e => e.TenantId)
            .HasDatabaseName("ix_retention_runs_tenant");

        builder.HasIndex(e => new { e.TenantId, e.Status })
            .HasDatabaseName("ix_retention_runs_status");

        builder.HasIndex(e => new { e.TenantId, e.StartedAt })
            .HasDatabaseName("ix_retention_runs_started");
    }
}
