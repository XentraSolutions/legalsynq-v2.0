using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Xenia.Domain.Configuration;

namespace Xenia.Infrastructure.Persistence.Configurations;

internal sealed class XeniaConfigurationEntryConfiguration : IEntityTypeConfiguration<XeniaConfigurationEntry>
{
    public void Configure(EntityTypeBuilder<XeniaConfigurationEntry> builder)
    {
        builder.ToTable("xn_configuration");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasColumnName("id")
            .HasColumnType("char(36)")
            .ValueGeneratedNever();

        builder.Property(e => e.ScopeType)
            .HasColumnName("scope_type")
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(e => e.ScopeId)
            .HasColumnName("scope_id")
            .HasMaxLength(300);

        builder.Property(e => e.Namespace)
            .HasColumnName("namespace")
            .HasMaxLength(XeniaConfigurationEntry.NamespaceMaxLength)
            .IsRequired();

        builder.Property(e => e.ConfigurationKey)
            .HasColumnName("configuration_key")
            .HasMaxLength(XeniaConfigurationEntry.KeyMaxLength)
            .IsRequired();

        builder.HasIndex(new[] { "ScopeType", "ScopeId", "Namespace", "ConfigurationKey" })
            .IsUnique()
            .HasDatabaseName("ix_xn_configuration_scope_key");

        builder.Property(e => e.ConfigurationValue)
            .HasColumnName("configuration_value")
            .HasMaxLength(XeniaConfigurationEntry.ValueMaxLength);

        builder.Property(e => e.ValueType)
            .HasColumnName("value_type")
            .HasMaxLength(XeniaConfigurationEntry.ValueTypeMaxLength);

        builder.Property(e => e.IsSecret)
            .HasColumnName("is_secret")
            .IsRequired();

        builder.Property(e => e.Version)
            .HasColumnName("version")
            .IsRequired();

        builder.Property(e => e.CreatedAtUtc)
            .HasColumnName("created_at")
            .HasColumnType("datetime(6)")
            .IsRequired();

        builder.Property(e => e.UpdatedAtUtc)
            .HasColumnName("updated_at")
            .HasColumnType("datetime(6)")
            .IsRequired();
    }
}
