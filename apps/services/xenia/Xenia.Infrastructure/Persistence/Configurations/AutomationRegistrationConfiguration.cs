using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Xenia.Domain.Automation;

namespace Xenia.Infrastructure.Persistence.Configurations;

internal sealed class AutomationRegistrationConfiguration
    : IEntityTypeConfiguration<AutomationRegistration>
{
    public void Configure(EntityTypeBuilder<AutomationRegistration> builder)
    {
        builder.ToTable("xn_automation_registry");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasColumnName("id")
            .HasColumnType("char(36)")
            .ValueGeneratedNever();

        builder.Property(e => e.AutomationKey)
            .HasColumnName("automation_key")
            .HasMaxLength(AutomationRegistration.AutomationKeyMaxLength)
            .IsRequired();

        builder.HasIndex(e => e.AutomationKey)
            .IsUnique()
            .HasDatabaseName("uq_xn_automation_registry_key");

        builder.Property(e => e.Provider)
            .HasColumnName("provider")
            .HasMaxLength(AutomationRegistration.ProviderMaxLength)
            .IsRequired();

        builder.Property(e => e.Category)
            .HasColumnName("category")
            .HasMaxLength(AutomationRegistration.CategoryMaxLength)
            .IsRequired();

        builder.Property(e => e.CurrentVersion)
            .HasColumnName("current_version")
            .HasMaxLength(AutomationRegistration.VersionMaxLength)
            .IsRequired();

        builder.Property(e => e.LifecycleStatus)
            .HasColumnName("lifecycle_status")
            .IsRequired();

        builder.HasIndex(e => e.LifecycleStatus)
            .HasDatabaseName("ix_xn_automation_registry_lifecycle_status");

        builder.Property(e => e.GloballyEnabled)
            .HasColumnName("globally_enabled")
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(e => e.ManifestHash)
            .HasColumnName("manifest_hash")
            .HasMaxLength(AutomationRegistration.ManifestHashMaxLength)
            .IsRequired();

        builder.Property(e => e.MinimumPlatformVersion)
            .HasColumnName("minimum_platform_version")
            .HasMaxLength(AutomationRegistration.PlatformVersionMaxLength);

        builder.Property(e => e.RegisteredAt)
            .HasColumnName("registered_at")
            .HasColumnType("datetime(6)")
            .IsRequired();

        builder.Property(e => e.LastReconciledAt)
            .HasColumnName("last_reconciled_at")
            .HasColumnType("datetime(6)");

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
