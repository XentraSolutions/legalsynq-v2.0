using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Xenia.Domain.Configuration;

namespace Xenia.Infrastructure.Persistence.Configurations;

internal sealed class XeniaTenantSettingsConfiguration : IEntityTypeConfiguration<XeniaTenantSettings>
{
    public void Configure(EntityTypeBuilder<XeniaTenantSettings> builder)
    {
        builder.ToTable("xn_tenant_settings");

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
            .IsUnique()
            .HasDatabaseName("ix_xn_tenant_settings_tenant_id");

        builder.Property(e => e.Enabled)
            .HasColumnName("enabled")
            .IsRequired();

        builder.Property(e => e.Settings)
            .HasColumnName("settings")
            .HasMaxLength(XeniaTenantSettings.SettingsMaxLength);

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
