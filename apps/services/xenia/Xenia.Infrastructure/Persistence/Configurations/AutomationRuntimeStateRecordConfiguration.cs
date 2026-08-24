using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Xenia.Domain.Automation;

namespace Xenia.Infrastructure.Persistence.Configurations;

internal sealed class AutomationRuntimeStateRecordConfiguration
    : IEntityTypeConfiguration<AutomationRuntimeStateRecord>
{
    // Pomelo 8 + EF 8: HasConversion<string>() triggers a NullRef via FindCollectionMapping
    // because string implements IEnumerable<char>.  Use explicit EnumToStringConverter<T>
    // instances to bypass the generic lookup and avoid the crash.

    // Custom converter for nullable lifecycle state (TenantState override).

    public void Configure(EntityTypeBuilder<AutomationRuntimeStateRecord> builder)
    {
        builder.ToTable("xn_automation_runtime_state");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasColumnName("id")
            .HasColumnType("char(36)")
            .ValueGeneratedNever();

        builder.Property(e => e.TenantId)
            .HasColumnName("tenant_id")
            .HasColumnType("char(36)")
            .IsRequired();

        builder.Property(e => e.AutomationKey)
            .HasColumnName("automation_key")
            .HasMaxLength(AutomationRuntimeStateRecord.AutomationKeyMaxLength)
            .IsRequired();

        builder.HasIndex(e => new { e.TenantId, e.AutomationKey })
            .IsUnique()
            .HasDatabaseName("uq_xn_automation_runtime_state_tenant_key");

        builder.HasIndex(e => e.TenantId)
            .HasDatabaseName("ix_xn_automation_runtime_state_tenant");

        builder.Property(e => e.AutomationVersion)
            .HasColumnName("automation_version")
            .HasMaxLength(AutomationRuntimeStateRecord.AutomationVersionMaxLength)
            .IsRequired()
            .HasDefaultValue(string.Empty);

        builder.Property(e => e.GlobalState)
            .HasColumnName("global_state")
            .IsRequired();

        builder.Property(e => e.TenantState)
            .HasColumnName("tenant_state");

        builder.Property(e => e.LifecycleState)
            .HasColumnName("lifecycle_state")
            .IsRequired();

        builder.Property(e => e.HealthState)
            .HasColumnName("health_state")
            .IsRequired();

        builder.HasIndex(e => e.HealthState)
            .HasDatabaseName("ix_xn_automation_runtime_state_health");

        builder.Property(e => e.LastExecutionAt)
            .HasColumnName("last_execution_at")
            .HasColumnType("datetime(6)");

        builder.Property(e => e.LastSuccessfulExecutionAt)
            .HasColumnName("last_successful_execution_at")
            .HasColumnType("datetime(6)");

        builder.Property(e => e.ConsecutiveFailureCount)
            .HasColumnName("consecutive_failure_count")
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(e => e.TotalExecutions)
            .HasColumnName("total_executions")
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(e => e.ActiveExecutions)
            .HasColumnName("active_executions")
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(e => e.TotalFailureCount)
            .HasColumnName("total_failure_count")
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(e => e.NextEligibleExecutionAt)
            .HasColumnName("next_eligible_execution_at")
            .HasColumnType("datetime(6)");

        builder.HasIndex(e => new { e.TenantId, e.NextEligibleExecutionAt })
            .HasDatabaseName("ix_xn_automation_runtime_state_next_eligible");

        builder.Property(e => e.LastSafeErrorCategory)
            .HasColumnName("last_safe_error_category")
            .HasMaxLength(AutomationRuntimeStateRecord.ErrorCategoryMaxLength);

        builder.Property(e => e.LastSafeErrorSummary)
            .HasColumnName("last_safe_error_summary")
            .HasMaxLength(AutomationRuntimeStateRecord.ErrorSummaryMaxLength);

        builder.Property(e => e.WorkerInstanceId)
            .HasColumnName("worker_instance_id")
            .HasMaxLength(AutomationRuntimeStateRecord.WorkerInstanceIdMaxLength);

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
