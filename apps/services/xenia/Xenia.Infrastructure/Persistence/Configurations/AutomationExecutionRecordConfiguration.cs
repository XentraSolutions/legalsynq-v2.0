using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Xenia.Domain.Automation;

namespace Xenia.Infrastructure.Persistence.Configurations;

internal sealed class AutomationExecutionRecordConfiguration
    : IEntityTypeConfiguration<AutomationExecutionRecord>
{
    public void Configure(EntityTypeBuilder<AutomationExecutionRecord> builder)
    {
        builder.ToTable("xn_automation_executions");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasColumnName("id")
            .HasColumnType("char(36)")
            .ValueGeneratedNever();

        builder.Property(e => e.ExecutionId)
            .HasColumnName("execution_id")
            .HasColumnType("char(36)")
            .IsRequired();

        builder.HasIndex(e => e.ExecutionId)
            .IsUnique()
            .HasDatabaseName("uq_xn_automation_executions_execution_id");

        builder.Property(e => e.TenantId)
            .HasColumnName("tenant_id")
            .HasColumnType("char(36)")
            .IsRequired();

        builder.HasIndex(e => e.TenantId)
            .HasDatabaseName("ix_xn_automation_executions_tenant");

        builder.Property(e => e.AutomationKey)
            .HasColumnName("automation_key")
            .HasMaxLength(AutomationExecutionRecord.AutomationKeyMaxLength)
            .IsRequired();

        builder.HasIndex(e => new { e.TenantId, e.AutomationKey })
            .HasDatabaseName("ix_xn_automation_executions_tenant_key");

        builder.Property(e => e.AutomationVersion)
            .HasColumnName("automation_version")
            .HasMaxLength(AutomationExecutionRecord.VersionMaxLength)
            .IsRequired();

        builder.Property(e => e.TriggerType)
            .HasColumnName("trigger_type")
            .IsRequired();

        builder.Property(e => e.Status)
            .HasColumnName("status")
            .IsRequired();

        builder.HasIndex(e => new { e.TenantId, e.Status })
            .HasDatabaseName("ix_xn_automation_executions_tenant_status");

        builder.Property(e => e.IdempotencyKey)
            .HasColumnName("idempotency_key")
            .HasMaxLength(AutomationExecutionRecord.IdempotencyKeyMaxLength);

        builder.Property(e => e.CorrelationId)
            .HasColumnName("correlation_id")
            .HasColumnType("char(36)");

        builder.HasIndex(e => e.CorrelationId)
            .HasDatabaseName("ix_xn_automation_executions_correlation");

        builder.Property(e => e.ActorId)
            .HasColumnName("actor_id")
            .HasMaxLength(AutomationExecutionRecord.ActorIdMaxLength);

        builder.Property(e => e.QueuedAt)
            .HasColumnName("queued_at")
            .HasColumnType("datetime(6)")
            .IsRequired();

        builder.Property(e => e.StartedAt)
            .HasColumnName("started_at")
            .HasColumnType("datetime(6)");

        builder.Property(e => e.CompletedAt)
            .HasColumnName("completed_at")
            .HasColumnType("datetime(6)");

        builder.Property(e => e.RetryCount)
            .HasColumnName("retry_count")
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(e => e.ParentExecutionId)
            .HasColumnName("parent_execution_id")
            .HasColumnType("char(36)");

        builder.Property(e => e.DeadLetterId)
            .HasColumnName("dead_letter_id")
            .HasColumnType("char(36)");

        builder.Property(e => e.SafeResultSummary)
            .HasColumnName("safe_result_summary")
            .HasMaxLength(AutomationExecutionRecord.SafeSummaryMaxLength);

        builder.Property(e => e.SafeErrorCategory)
            .HasColumnName("safe_error_category")
            .HasMaxLength(AutomationExecutionRecord.ErrorCategoryMaxLength);

        builder.Property(e => e.SafeErrorSummary)
            .HasColumnName("safe_error_summary")
            .HasMaxLength(AutomationExecutionRecord.SafeSummaryMaxLength);

        builder.Property(e => e.WorkerInstanceId)
            .HasColumnName("worker_instance_id")
            .HasMaxLength(AutomationExecutionRecord.WorkerInstanceIdMaxLength);

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
