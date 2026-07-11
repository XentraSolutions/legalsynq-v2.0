using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Xenia.Domain.Automation;

namespace Xenia.Infrastructure.Persistence.Configurations;

internal sealed class AutomationScheduleRecordConfiguration
    : IEntityTypeConfiguration<AutomationScheduleRecord>
{
    public void Configure(EntityTypeBuilder<AutomationScheduleRecord> builder)
    {
        builder.ToTable("xn_automation_schedules");

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
            .HasDatabaseName("ix_xn_automation_schedules_tenant");

        builder.Property(e => e.AutomationKey)
            .HasColumnName("automation_key")
            .HasMaxLength(AutomationScheduleRecord.AutomationKeyMaxLength)
            .IsRequired();

        builder.HasIndex(e => new { e.TenantId, e.AutomationKey })
            .HasDatabaseName("ix_xn_automation_schedules_tenant_key");

        builder.Property(e => e.ScheduleType)
            .HasColumnName("schedule_type")
            .IsRequired();

        builder.Property(e => e.Expression)
            .HasColumnName("expression")
            .HasMaxLength(AutomationScheduleRecord.ExpressionMaxLength);

        builder.Property(e => e.IntervalSeconds)
            .HasColumnName("interval_seconds");

        builder.Property(e => e.TimeZone)
            .HasColumnName("time_zone")
            .HasMaxLength(AutomationScheduleRecord.TimeZoneMaxLength)
            .IsRequired();

        builder.Property(e => e.Enabled)
            .HasColumnName("enabled")
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(e => e.NextRunAt)
            .HasColumnName("next_run_at")
            .HasColumnType("datetime(6)");

        builder.HasIndex(e => new { e.Enabled, e.NextRunAt })
            .HasDatabaseName("ix_xn_automation_schedules_enabled_next_run");

        builder.Property(e => e.LastRunAt)
            .HasColumnName("last_run_at")
            .HasColumnType("datetime(6)");

        builder.Property(e => e.MisfirePolicy)
            .HasColumnName("misfire_policy")
            .IsRequired();

        builder.Property(e => e.ConcurrencyPolicy)
            .HasColumnName("concurrency_policy")
            .IsRequired();

        builder.Property(e => e.CreatedBy)
            .HasColumnName("created_by")
            .HasMaxLength(AutomationScheduleRecord.ActorMaxLength);

        builder.Property(e => e.UpdatedBy)
            .HasColumnName("updated_by")
            .HasMaxLength(AutomationScheduleRecord.ActorMaxLength);

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
