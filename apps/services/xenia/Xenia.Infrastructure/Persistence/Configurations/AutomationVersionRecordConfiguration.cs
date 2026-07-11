using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Xenia.Domain.Automation;

namespace Xenia.Infrastructure.Persistence.Configurations;

internal sealed class AutomationVersionRecordConfiguration
    : IEntityTypeConfiguration<AutomationVersionRecord>
{
    public void Configure(EntityTypeBuilder<AutomationVersionRecord> builder)
    {
        builder.ToTable("xn_automation_versions");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasColumnName("id")
            .HasColumnType("char(36)")
            .ValueGeneratedNever();

        builder.Property(e => e.AutomationKey)
            .HasColumnName("automation_key")
            .HasMaxLength(AutomationVersionRecord.AutomationKeyMaxLength)
            .IsRequired();

        builder.Property(e => e.Version)
            .HasColumnName("version")
            .HasMaxLength(AutomationVersionRecord.VersionMaxLength)
            .IsRequired();

        builder.HasIndex(e => new { e.AutomationKey, e.Version })
            .IsUnique()
            .HasDatabaseName("uq_xn_automation_versions_key_version");

        builder.HasIndex(e => e.AutomationKey)
            .HasDatabaseName("ix_xn_automation_versions_key");

        builder.Property(e => e.ManifestJson)
            .HasColumnName("manifest_json")
            .HasColumnType("longtext")
            .IsRequired();

        builder.Property(e => e.ManifestSchemaVersion)
            .HasColumnName("manifest_schema_version")
            .HasMaxLength(AutomationVersionRecord.ManifestSchemaVersionMaxLength)
            .IsRequired();

        builder.Property(e => e.CompatibilityJson)
            .HasColumnName("compatibility_json")
            .HasColumnType("text");

        builder.Property(e => e.RegisteredAt)
            .HasColumnName("registered_at")
            .HasColumnType("datetime(6)")
            .IsRequired();

        builder.Property(e => e.ActivatedAt)
            .HasColumnName("activated_at")
            .HasColumnType("datetime(6)");

        builder.Property(e => e.RetiredAt)
            .HasColumnName("retired_at")
            .HasColumnType("datetime(6)");

        builder.Property(e => e.Status)
            .HasColumnName("status")
            .IsRequired();

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
