using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Xenia.Domain.Email;

namespace Xenia.Infrastructure.Persistence.Configurations;

internal sealed class EmailIngestionRunConfiguration : IEntityTypeConfiguration<EmailIngestionRun>
{
    private static readonly EnumToStringConverter<IngestionRunStatus>      _statusConverter  = new();
    private static readonly EnumToStringConverter<IngestionRunTriggerType> _triggerConverter = new();

    public void Configure(EntityTypeBuilder<EmailIngestionRun> builder)
    {
        builder.ToTable("xn_email_ingestion_runs");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasColumnName("id").HasColumnType("char(36)").ValueGeneratedNever();

        builder.Property(e => e.TenantId)
            .HasColumnName("tenant_id").HasColumnType("char(36)").IsRequired();

        builder.Property(e => e.EmailSourceId)
            .HasColumnName("email_source_id").HasColumnType("char(36)").IsRequired();

        builder.Property(e => e.TriggerType)
            .HasColumnName("trigger_type")
            .HasConversion(_triggerConverter).HasMaxLength(32).IsRequired();

        builder.Property(e => e.Status)
            .HasColumnName("status")
            .HasConversion(_statusConverter).HasMaxLength(32).IsRequired();

        builder.Property(e => e.StartedAt)
            .HasColumnName("started_at").HasColumnType("datetime(6)").IsRequired();

        builder.Property(e => e.CompletedAt)
            .HasColumnName("completed_at").HasColumnType("datetime(6)");

        builder.Property(e => e.DurationMs)
            .HasColumnName("duration_ms");

        builder.Property(e => e.CorrelationId)
            .HasColumnName("correlation_id").HasMaxLength(EmailIngestionRun.CorrelationIdMaxLength);

        builder.Property(e => e.ActorId)
            .HasColumnName("actor_id").HasColumnType("char(36)");

        builder.Property(e => e.WorkerInstanceId)
            .HasColumnName("worker_instance_id").HasMaxLength(EmailIngestionRun.WorkerInstanceIdMaxLength);

        builder.Property(e => e.MessagesDiscovered)
            .HasColumnName("messages_discovered").IsRequired();

        builder.Property(e => e.MessagesImported)
            .HasColumnName("messages_imported").IsRequired();

        builder.Property(e => e.MessagesUpdated)
            .HasColumnName("messages_updated").IsRequired();

        builder.Property(e => e.MessagesDuplicated)
            .HasColumnName("messages_duplicated").IsRequired();

        builder.Property(e => e.MessagesFailed)
            .HasColumnName("messages_failed").IsRequired();

        builder.Property(e => e.AttachmentsDiscovered)
            .HasColumnName("attachments_discovered").IsRequired();

        builder.Property(e => e.AttachmentsDispatched)
            .HasColumnName("attachments_dispatched").IsRequired();

        builder.Property(e => e.AttachmentsFailed)
            .HasColumnName("attachments_failed").IsRequired();

        builder.Property(e => e.PagesProcessed)
            .HasColumnName("pages_processed").IsRequired();

        builder.Property(e => e.RetryCount)
            .HasColumnName("retry_count").IsRequired();

        builder.Property(e => e.CursorBeforeSafeSummary)
            .HasColumnName("cursor_before_safe_summary").HasMaxLength(EmailIngestionRun.CursorSummaryMaxLength);

        builder.Property(e => e.CursorAfterSafeSummary)
            .HasColumnName("cursor_after_safe_summary").HasMaxLength(EmailIngestionRun.CursorSummaryMaxLength);

        builder.Property(e => e.ErrorCode)
            .HasColumnName("error_code").HasMaxLength(EmailIngestionRun.ErrorCodeMaxLength);

        builder.Property(e => e.SafeErrorSummary)
            .HasColumnName("safe_error_summary").HasMaxLength(EmailIngestionRun.SafeErrorSummaryMaxLength);

        builder.Property(e => e.CreatedAtUtc)
            .HasColumnName("created_at_utc").HasColumnType("datetime(6)").IsRequired();

        builder.Property(e => e.UpdatedAtUtc)
            .HasColumnName("updated_at_utc").HasColumnType("datetime(6)").IsRequired();

        // Indexes
        builder.HasIndex(e => e.TenantId).HasDatabaseName("ix_ingestion_runs_tenant");
        builder.HasIndex(e => new { e.TenantId, e.EmailSourceId }).HasDatabaseName("ix_ingestion_runs_source");
        builder.HasIndex(e => new { e.TenantId, e.Status }).HasDatabaseName("ix_ingestion_runs_status");
        builder.HasIndex(e => new { e.TenantId, e.StartedAt }).HasDatabaseName("ix_ingestion_runs_started_at");
    }
}
