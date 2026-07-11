using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Xenia.Domain.Email;

namespace Xenia.Infrastructure.Persistence.Configurations;

internal sealed class EmailProviderSettingsConfiguration : IEntityTypeConfiguration<EmailProviderSettings>
{

    public void Configure(EntityTypeBuilder<EmailProviderSettings> builder)
    {
        builder.ToTable("xn_email_provider_settings");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id)
            .HasColumnName("id")
            .HasColumnType("char(36)")
            .ValueGeneratedNever();

        builder.Property(e => e.TenantId)
            .HasColumnName("tenant_id")
            .HasColumnType("char(36)")
            .IsRequired();

        builder.Property(e => e.EmailSourceId)
            .HasColumnName("email_source_id")
            .HasColumnType("char(36)")
            .IsRequired();

        builder.Property(e => e.ProviderType)
            .HasColumnName("provider_type")
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(e => e.ConfigurationJson)
            .HasColumnName("configuration_json")
            .HasMaxLength(EmailProviderSettings.ConfigurationJsonMaxLength);

        builder.Property(e => e.ConfigurationVersion)
            .HasColumnName("configuration_version")
            .IsRequired();

        builder.Property(e => e.CreatedAtUtc)
            .HasColumnName("created_at")
            .HasColumnType("datetime(6)")
            .IsRequired();

        builder.Property(e => e.UpdatedAtUtc)
            .HasColumnName("updated_at")
            .HasColumnType("datetime(6)")
            .IsRequired();

        builder.HasIndex(e => e.EmailSourceId)
            .IsUnique()
            .HasDatabaseName("ix_xn_email_prov_settings_source");

        builder.HasIndex(e => e.TenantId)
            .HasDatabaseName("ix_xn_email_prov_settings_tenant");
    }
}
