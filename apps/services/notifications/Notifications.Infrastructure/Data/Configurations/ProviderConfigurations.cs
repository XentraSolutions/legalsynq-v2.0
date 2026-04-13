using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Notifications.Domain;

namespace Notifications.Infrastructure.Data.Configurations;

public class TenantProviderConfigConfiguration : IEntityTypeConfiguration<TenantProviderConfig>
{
    public void Configure(EntityTypeBuilder<TenantProviderConfig> builder)
    {
        builder.ToTable("ntf_tenant_provider_configs");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.TenantId).HasColumnName("tenant_id");
        builder.Property(e => e.Channel).HasColumnName("channel").HasMaxLength(20);
        builder.Property(e => e.ProviderType).HasColumnName("provider_type").HasMaxLength(50);
        builder.Property(e => e.DisplayName).HasColumnName("display_name").HasMaxLength(200);
        builder.Property(e => e.CredentialsJson).HasColumnName("credentials_json").HasColumnType("text");
        builder.Property(e => e.SettingsJson).HasColumnName("settings_json").HasColumnType("text");
        builder.Property(e => e.Status).HasColumnName("status").HasMaxLength(20).HasDefaultValue("active");
        builder.Property(e => e.ValidationStatus).HasColumnName("validation_status").HasMaxLength(30).HasDefaultValue("not_validated");
        builder.Property(e => e.ValidationMessage).HasColumnName("validation_message").HasColumnType("text");
        builder.Property(e => e.LastValidatedAt).HasColumnName("last_validated_at");
        builder.Property(e => e.HealthStatus).HasColumnName("health_status").HasMaxLength(20).HasDefaultValue("unknown");
        builder.Property(e => e.LastHealthCheckAt).HasColumnName("last_health_check_at");
        builder.Property(e => e.HealthCheckLatencyMs).HasColumnName("health_check_latency_ms");
        builder.Property(e => e.Priority).HasColumnName("priority").HasDefaultValue(1);
        builder.Property(e => e.CreatedAt).HasColumnName("created_at");
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at");

        builder.HasIndex(e => new { e.TenantId, e.Channel }).HasDatabaseName("idx_tenant_provider_configs_tenant_channel");
    }
}

public class TenantChannelProviderSettingConfiguration : IEntityTypeConfiguration<TenantChannelProviderSetting>
{
    public void Configure(EntityTypeBuilder<TenantChannelProviderSetting> builder)
    {
        builder.ToTable("ntf_tenant_channel_provider_settings");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.TenantId).HasColumnName("tenant_id");
        builder.Property(e => e.Channel).HasColumnName("channel").HasMaxLength(20);
        builder.Property(e => e.ProviderMode).HasColumnName("provider_mode").HasMaxLength(30).HasDefaultValue("platform_managed");
        builder.Property(e => e.PrimaryTenantProviderConfigId).HasColumnName("primary_tenant_provider_config_id");
        builder.Property(e => e.FallbackTenantProviderConfigId).HasColumnName("fallback_tenant_provider_config_id");
        builder.Property(e => e.AllowPlatformFallback).HasColumnName("allow_platform_fallback").HasDefaultValue(true);
        builder.Property(e => e.AllowAutomaticFailover).HasColumnName("allow_automatic_failover").HasDefaultValue(true);
        builder.Property(e => e.CreatedAt).HasColumnName("created_at");
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at");

        builder.HasIndex(e => new { e.TenantId, e.Channel })
            .HasDatabaseName("uq_tenant_channel_settings")
            .IsUnique();
    }
}

public class ProviderHealthConfiguration : IEntityTypeConfiguration<ProviderHealth>
{
    public void Configure(EntityTypeBuilder<ProviderHealth> builder)
    {
        builder.ToTable("ntf_provider_health");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.ProviderType).HasColumnName("provider_type").HasMaxLength(50);
        builder.Property(e => e.Channel).HasColumnName("channel").HasMaxLength(20);
        builder.Property(e => e.OwnershipMode).HasColumnName("ownership_mode").HasMaxLength(20).HasDefaultValue("platform");
        builder.Property(e => e.TenantProviderConfigId).HasColumnName("tenant_provider_config_id");
        builder.Property(e => e.HealthStatus).HasColumnName("health_status").HasMaxLength(20).HasDefaultValue("healthy");
        builder.Property(e => e.ConsecutiveFailures).HasColumnName("consecutive_failures").HasDefaultValue(0);
        builder.Property(e => e.ConsecutiveSuccesses).HasColumnName("consecutive_successes").HasDefaultValue(0);
        builder.Property(e => e.LastLatencyMs).HasColumnName("last_latency_ms");
        builder.Property(e => e.LastCheckAt).HasColumnName("last_check_at");
        builder.Property(e => e.LastFailureAt).HasColumnName("last_failure_at");
        builder.Property(e => e.LastRecoveryAt).HasColumnName("last_recovery_at");
        builder.Property(e => e.CreatedAt).HasColumnName("created_at");
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at");
    }
}

public class ProviderWebhookLogConfiguration : IEntityTypeConfiguration<ProviderWebhookLog>
{
    public void Configure(EntityTypeBuilder<ProviderWebhookLog> builder)
    {
        builder.ToTable("ntf_provider_webhook_logs");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.Provider).HasColumnName("provider").HasMaxLength(50);
        builder.Property(e => e.Channel).HasColumnName("channel").HasMaxLength(20);
        builder.Property(e => e.RequestHeadersJson).HasColumnName("request_headers_json").HasColumnType("text");
        builder.Property(e => e.PayloadJson).HasColumnName("payload_json").HasColumnType("longtext");
        builder.Property(e => e.SignatureVerified).HasColumnName("signature_verified");
        builder.Property(e => e.ProcessingStatus).HasColumnName("processing_status").HasMaxLength(20).HasDefaultValue("received");
        builder.Property(e => e.ErrorMessage).HasColumnName("error_message").HasColumnType("text");
        builder.Property(e => e.ReceivedAt).HasColumnName("received_at");
        builder.Property(e => e.CreatedAt).HasColumnName("created_at");
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at");
    }
}
