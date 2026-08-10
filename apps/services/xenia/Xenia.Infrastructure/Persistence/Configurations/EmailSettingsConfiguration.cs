using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Xenia.Domain.Email;

namespace Xenia.Infrastructure.Persistence.Configurations;

internal sealed class EmailSettingsConfiguration : IEntityTypeConfiguration<EmailSettings>
{
    public void Configure(EntityTypeBuilder<EmailSettings> builder)
    {
        builder.ToTable("xn_email_settings");

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
            .HasDatabaseName("ix_xn_email_settings_tenant_id_unique");

        builder.Property(e => e.ConnectionTimeoutSeconds)
            .HasColumnName("connection_timeout_seconds")
            .IsRequired()
            .HasDefaultValue(EmailSettings.DefaultConnectionTimeoutSeconds);

        builder.Property(e => e.AllowedProviderTypes)
            .HasColumnName("allowed_provider_types")
            .HasMaxLength(EmailSettings.AllowedProviderTypesMaxLength)
            .IsRequired();

        builder.Property(e => e.ValidationRetryLimit)
            .HasColumnName("validation_retry_limit")
            .IsRequired()
            .HasDefaultValue(EmailSettings.DefaultValidationRetryLimit);

        builder.Property(e => e.ValidationHistoryRetentionDays)
            .HasColumnName("validation_history_retention_days")
            .IsRequired()
            .HasDefaultValue(EmailSettings.DefaultValidationHistoryRetentionDays);

        builder.Property(e => e.AllowedPorts)
            .HasColumnName("allowed_ports")
            .HasMaxLength(EmailSettings.AllowedPortsMaxLength)
            .IsRequired();

        builder.Property(e => e.RequireTls)
            .HasColumnName("require_tls")
            .IsRequired()
            .HasDefaultValue(true);

        builder.Property(e => e.AllowCustomHosts)
            .HasColumnName("allow_custom_hosts")
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(e => e.SsrfPolicyMode)
            .HasColumnName("ssrf_policy_mode")
            .HasMaxLength(EmailSettings.SsrfPolicyModeMaxLength)
            .IsRequired();

        builder.Property(e => e.DefaultSourceEnabled)
            .HasColumnName("default_source_enabled")
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(e => e.Version)
            .HasColumnName("version")
            .IsRequired();

        builder.Property(e => e.UpdatedBy)
            .HasColumnName("updated_by")
            .HasColumnType("char(36)");

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
