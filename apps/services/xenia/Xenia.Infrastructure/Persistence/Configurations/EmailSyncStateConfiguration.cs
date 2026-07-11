using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Xenia.Domain.Email;

namespace Xenia.Infrastructure.Persistence.Configurations;

internal sealed class EmailSyncStateConfiguration : IEntityTypeConfiguration<EmailSyncState>
{

    public void Configure(EntityTypeBuilder<EmailSyncState> builder)
    {
        builder.ToTable("xn_email_sync_state");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasColumnName("id").HasColumnType("char(36)").ValueGeneratedNever();

        builder.Property(e => e.TenantId)
            .HasColumnName("tenant_id").HasColumnType("char(36)").IsRequired();

        builder.Property(e => e.EmailSourceId)
            .HasColumnName("email_source_id").HasColumnType("char(36)").IsRequired();

        builder.Property(e => e.ProviderType)
            .HasColumnName("provider_type")
            .IsRequired();

        builder.Property(e => e.CursorType)
            .HasColumnName("cursor_type")
            .IsRequired();

        builder.Property(e => e.CursorValue)
            .HasColumnName("cursor_value").HasMaxLength(EmailSyncState.CursorValueMaxLength);

        builder.Property(e => e.CursorMetadataJson)
            .HasColumnName("cursor_metadata_json").HasMaxLength(EmailSyncState.CursorMetadataMaxLength);

        builder.Property(e => e.SafeCursorSummary)
            .HasColumnName("safe_cursor_summary").HasMaxLength(EmailSyncState.SafeCursorSummaryMaxLength);

        builder.Property(e => e.LastSuccessfulSyncAt)
            .HasColumnName("last_successful_sync_at").HasColumnType("datetime(6)");

        builder.Property(e => e.LastAttemptedSyncAt)
            .HasColumnName("last_attempted_sync_at").HasColumnType("datetime(6)");

        builder.Property(e => e.LastProcessedProviderTimestamp)
            .HasColumnName("last_processed_provider_timestamp").HasColumnType("datetime(6)");

        builder.Property(e => e.LastProcessedProviderMessageId)
            .HasColumnName("last_processed_provider_message_id").HasMaxLength(EmailMessage.ProviderMessageIdMaxLength);

        builder.Property(e => e.InitialSyncCompleted)
            .HasColumnName("initial_sync_completed").IsRequired();

        builder.Property(e => e.ConsecutiveFailureCount)
            .HasColumnName("consecutive_failure_count").IsRequired();

        builder.Property(e => e.NextEligibleSyncAt)
            .HasColumnName("next_eligible_sync_at").HasColumnType("datetime(6)");

        builder.Property(e => e.LastErrorCode)
            .HasColumnName("last_error_code").HasMaxLength(EmailSyncState.ErrorCodeMaxLength);

        builder.Property(e => e.SafeLastErrorSummary)
            .HasColumnName("safe_last_error_summary").HasMaxLength(EmailSyncState.SafeErrorSummaryMaxLength);

        builder.Property(e => e.StateVersion)
            .HasColumnName("state_version").IsRequired()
            .IsConcurrencyToken();

        builder.Property(e => e.CreatedAtUtc)
            .HasColumnName("created_at_utc").HasColumnType("datetime(6)").IsRequired();

        builder.Property(e => e.UpdatedAtUtc)
            .HasColumnName("updated_at_utc").HasColumnType("datetime(6)").IsRequired();

        // One sync state per source
        builder.HasIndex(e => e.EmailSourceId)
            .IsUnique()
            .HasDatabaseName("ux_email_sync_state_source_unique");

        builder.HasIndex(e => e.TenantId).HasDatabaseName("ix_email_sync_state_tenant");
        builder.HasIndex(e => new { e.TenantId, e.NextEligibleSyncAt }).HasDatabaseName("ix_email_sync_state_next_eligible");
        builder.HasIndex(e => new { e.TenantId, e.LastSuccessfulSyncAt }).HasDatabaseName("ix_email_sync_state_last_success");
    }
}
