using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Xenia.Domain.Modules;

namespace Xenia.Infrastructure.Persistence.Configurations;

internal sealed class XeniaModuleConfiguration : IEntityTypeConfiguration<XeniaModule>
{
    public void Configure(EntityTypeBuilder<XeniaModule> builder)
    {
        builder.ToTable("xn_modules");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasColumnName("id")
            .HasColumnType("char(36)")
            .ValueGeneratedNever();

        builder.Property(e => e.ModuleKey)
            .HasColumnName("module_key")
            .HasMaxLength(XeniaModule.KeyMaxLength)
            .IsRequired();

        builder.HasIndex(e => e.ModuleKey)
            .IsUnique()
            .HasDatabaseName("ix_xn_modules_module_key");

        builder.Property(e => e.Name)
            .HasColumnName("name")
            .HasMaxLength(XeniaModule.NameMaxLength)
            .IsRequired();

        builder.Property(e => e.Version)
            .HasColumnName("version")
            .HasMaxLength(XeniaModule.VersionMaxLength)
            .IsRequired();

        builder.Property(e => e.Description)
            .HasColumnName("description")
            .HasMaxLength(XeniaModule.DescriptionMaxLength);

        builder.Property(e => e.GlobalEnabled)
            .HasColumnName("global_enabled")
            .IsRequired();

        builder.Property(e => e.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(e => e.ConfigurationNamespace)
            .HasColumnName("configuration_namespace")
            .HasMaxLength(XeniaModule.NamespaceMaxLength)
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
