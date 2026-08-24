using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Xenia.Domain.Automation;

namespace Xenia.Infrastructure.Persistence.Configurations;

internal sealed class TenantAutomationStateConfiguration
    : IEntityTypeConfiguration<TenantAutomationState>
{
    public void Configure(EntityTypeBuilder<TenantAutomationState> builder)
    {
        builder.ToTable("xn_tenant_automations");

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
            .HasMaxLength(TenantAutomationState.AutomationKeyMaxLength)
            .IsRequired();

        builder.HasIndex(e => new { e.TenantId, e.AutomationKey })
            .IsUnique()
            .HasDatabaseName("uq_xn_tenant_automations_tenant_key");

        builder.HasIndex(e => e.TenantId)
            .HasDatabaseName("ix_xn_tenant_automations_tenant");

        builder.Property(e => e.Enabled)
            .HasColumnName("enabled")
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(e => e.LifecycleOverride)
            .HasColumnName("lifecycle_override")
            .HasMaxLength(TenantAutomationState.LifecycleOverrideMaxLength);

        builder.Property(e => e.ConfigurationVersion)
            .HasColumnName("configuration_version")
            .HasMaxLength(TenantAutomationState.ConfigVersionMaxLength);

        builder.Property(e => e.LastValidatedAt)
            .HasColumnName("last_validated_at")
            .HasColumnType("datetime(6)");

        builder.Property(e => e.UpdatedBy)
            .HasColumnName("updated_by")
            .HasMaxLength(TenantAutomationState.UpdatedByMaxLength);

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
