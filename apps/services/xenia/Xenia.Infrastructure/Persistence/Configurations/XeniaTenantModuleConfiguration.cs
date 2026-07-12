using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Xenia.Domain.Modules;

namespace Xenia.Infrastructure.Persistence.Configurations;

internal sealed class XeniaTenantModuleConfiguration : IEntityTypeConfiguration<XeniaTenantModule>
{
    public void Configure(EntityTypeBuilder<XeniaTenantModule> builder)
    {
        builder.ToTable("xn_tenant_modules");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasColumnName("id")
            .HasColumnType("char(36)")
            .ValueGeneratedNever();

        builder.Property(e => e.TenantId)
            .HasColumnName("tenant_id")
            .HasColumnType("char(36)")
            .IsRequired();

        builder.Property(e => e.ModuleKey)
            .HasColumnName("module_key")
            .HasMaxLength(XeniaTenantModule.ModuleKeyMaxLength)
            .IsRequired();

        builder.HasIndex(new[] { "TenantId", "ModuleKey" })
            .IsUnique()
            .HasDatabaseName("ix_xn_tenant_modules_tenant_module");

        builder.HasIndex(e => e.TenantId)
            .HasDatabaseName("ix_xn_tenant_modules_tenant_id");

        builder.Property(e => e.Enabled)
            .HasColumnName("enabled")
            .IsRequired();

        builder.Property(e => e.ModuleConfiguration)
            .HasColumnName("module_configuration")
            .HasMaxLength(XeniaTenantModule.ConfigurationMaxLength);

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
