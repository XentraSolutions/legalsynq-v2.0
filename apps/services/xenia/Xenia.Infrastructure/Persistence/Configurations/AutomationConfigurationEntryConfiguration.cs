using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Xenia.Domain.Automation;

namespace Xenia.Infrastructure.Persistence.Configurations;

internal sealed class AutomationConfigurationEntryConfiguration
    : IEntityTypeConfiguration<AutomationConfigurationEntry>
{
    public void Configure(EntityTypeBuilder<AutomationConfigurationEntry> builder)
    {
        builder.ToTable("xn_automation_configuration");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasColumnName("id")
            .HasColumnType("char(36)")
            .ValueGeneratedNever();

        builder.Property(e => e.ScopeType)
            .HasColumnName("scope_type")
            .IsRequired();

        builder.Property(e => e.TenantId)
            .HasColumnName("tenant_id")
            .HasColumnType("char(36)");

        builder.Property(e => e.AutomationKey)
            .HasColumnName("automation_key")
            .HasMaxLength(AutomationConfigurationEntry.AutomationKeyMaxLength)
            .IsRequired();

        builder.Property(e => e.ConfigurationNamespace)
            .HasColumnName("configuration_namespace")
            .HasMaxLength(AutomationConfigurationEntry.NamespaceMaxLength)
            .IsRequired();

        builder.HasIndex(e => new { e.ScopeType, e.TenantId, e.AutomationKey, e.ConfigurationNamespace })
            .IsUnique()
            .HasDatabaseName("uq_xn_automation_configuration_scope");

        builder.HasIndex(e => e.TenantId)
            .HasDatabaseName("ix_xn_automation_configuration_tenant");

        builder.HasIndex(e => e.AutomationKey)
            .HasDatabaseName("ix_xn_automation_configuration_key");

        builder.Property(e => e.ConfigurationJson)
            .HasColumnName("configuration_json")
            .HasColumnType("longtext")
            .IsRequired();

        builder.Property(e => e.SchemaVersion)
            .HasColumnName("schema_version")
            .HasMaxLength(AutomationConfigurationEntry.SchemaVersionMaxLength)
            .IsRequired();

        builder.Property(e => e.SecretReferencesJson)
            .HasColumnName("secret_references_json")
            .HasColumnType("text");

        builder.Property(e => e.UpdatedBy)
            .HasColumnName("updated_by")
            .HasMaxLength(AutomationConfigurationEntry.UpdatedByMaxLength);

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
