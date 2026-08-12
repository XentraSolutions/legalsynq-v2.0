using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Xenia.Domain.Automation;

namespace Xenia.Infrastructure.Persistence.Configurations;

internal sealed class AutomationDeadLetterRecordConfiguration
    : IEntityTypeConfiguration<AutomationDeadLetterRecord>
{
    public void Configure(EntityTypeBuilder<AutomationDeadLetterRecord> builder)
    {
        builder.ToTable("xn_automation_dead_letters");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasColumnName("id")
            .HasColumnType("char(36)")
            .ValueGeneratedNever();

        builder.Property(e => e.TenantId)
            .HasColumnName("tenant_id")
            .HasColumnType("char(36)")
            .IsRequired();

        builder.HasIndex(e => e.TenantId)
            .HasDatabaseName("ix_xn_automation_dead_letters_tenant");

        builder.Property(e => e.AutomationKey)
            .HasColumnName("automation_key")
            .HasMaxLength(AutomationDeadLetterRecord.AutomationKeyMaxLength)
            .IsRequired();

        builder.HasIndex(e => new { e.TenantId, e.AutomationKey })
            .HasDatabaseName("ix_xn_automation_dead_letters_tenant_key");

        builder.Property(e => e.AutomationVersion)
            .HasColumnName("automation_version")
            .HasMaxLength(AutomationDeadLetterRecord.VersionMaxLength)
            .IsRequired();

        builder.Property(e => e.ExecutionId)
            .HasColumnName("execution_id")
            .HasColumnType("char(36)");

        builder.Property(e => e.TriggerType)
            .HasColumnName("trigger_type")
            .IsRequired();

        builder.Property(e => e.FailureCategory)
            .HasColumnName("failure_category")
            .HasMaxLength(AutomationDeadLetterRecord.FailureCategoryMaxLength)
            .IsRequired();

        builder.Property(e => e.SafeErrorSummary)
            .HasColumnName("safe_error_summary")
            .HasMaxLength(AutomationDeadLetterRecord.SafeErrorSummaryMaxLength);

        builder.Property(e => e.RetryCount)
            .HasColumnName("retry_count")
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(e => e.ReplayCount)
            .HasColumnName("replay_count")
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(e => e.FirstFailedAt)
            .HasColumnName("first_failed_at")
            .HasColumnType("datetime(6)")
            .IsRequired();

        builder.Property(e => e.LastFailedAt)
            .HasColumnName("last_failed_at")
            .HasColumnType("datetime(6)")
            .IsRequired();

        builder.Property(e => e.NextEligibleRetryAt)
            .HasColumnName("next_eligible_retry_at")
            .HasColumnType("datetime(6)");

        builder.HasIndex(e => new { e.TenantId, e.NextEligibleRetryAt })
            .HasDatabaseName("ix_xn_automation_dead_letters_next_retry");

        builder.Property(e => e.Status)
            .HasColumnName("status")
            .IsRequired();

        builder.HasIndex(e => new { e.TenantId, e.Status })
            .HasDatabaseName("ix_xn_automation_dead_letters_tenant_status");

        builder.Property(e => e.Resolution)
            .HasColumnName("resolution")
            .HasMaxLength(AutomationDeadLetterRecord.ResolutionMaxLength);

        builder.Property(e => e.CorrelationId)
            .HasColumnName("correlation_id")
            .HasColumnType("char(36)");

        builder.Property(e => e.CreatedAtUtc)
            .HasColumnName("created_at")
            .HasColumnType("datetime(6)")
            .IsRequired();

        builder.Property(e => e.UpdatedAtUtc)
            .HasColumnName("updated_at")
            .HasColumnType("datetime(6)")
            .IsRequired();

        builder.Property(e => e.RowVersion)
            .HasColumnName("row_version")
            .IsRequired()
            .IsConcurrencyToken();
    }
}
