using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Notifications.Domain;

namespace Notifications.Infrastructure.Data.Configurations;

public class TenantBillingPlanConfiguration : IEntityTypeConfiguration<TenantBillingPlan>
{
    public void Configure(EntityTypeBuilder<TenantBillingPlan> builder)
    {
        builder.ToTable("tenant_billing_plans");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.TenantId).HasColumnName("tenant_id");
        builder.Property(e => e.PlanName).HasColumnName("plan_name").HasMaxLength(200);
        builder.Property(e => e.BillingMode).HasColumnName("billing_mode").HasMaxLength(30);
        builder.Property(e => e.Status).HasColumnName("status").HasMaxLength(20).HasDefaultValue("active");
        builder.Property(e => e.MonthlyFlatRate).HasColumnName("monthly_flat_rate").HasColumnType("decimal(10,2)");
        builder.Property(e => e.Currency).HasColumnName("currency").HasMaxLength(10).HasDefaultValue("USD");
        builder.Property(e => e.EffectiveFrom).HasColumnName("effective_from");
        builder.Property(e => e.EffectiveTo).HasColumnName("effective_to");
        builder.Property(e => e.CreatedAt).HasColumnName("created_at");
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at");
    }
}

public class TenantBillingRateConfiguration : IEntityTypeConfiguration<TenantBillingRate>
{
    public void Configure(EntityTypeBuilder<TenantBillingRate> builder)
    {
        builder.ToTable("tenant_billing_rates");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.BillingPlanId).HasColumnName("billing_plan_id");
        builder.Property(e => e.UsageUnit).HasColumnName("usage_unit").HasMaxLength(100);
        builder.Property(e => e.Channel).HasColumnName("channel").HasMaxLength(20);
        builder.Property(e => e.ProviderOwnershipMode).HasColumnName("provider_ownership_mode").HasMaxLength(20);
        builder.Property(e => e.UnitPrice).HasColumnName("unit_price").HasColumnType("decimal(10,6)");
        builder.Property(e => e.Currency).HasColumnName("currency").HasMaxLength(10).HasDefaultValue("USD");
        builder.Property(e => e.IsBillable).HasColumnName("is_billable").HasDefaultValue(true);
        builder.Property(e => e.CreatedAt).HasColumnName("created_at");
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at");
    }
}

public class TenantRateLimitPolicyConfiguration : IEntityTypeConfiguration<TenantRateLimitPolicy>
{
    public void Configure(EntityTypeBuilder<TenantRateLimitPolicy> builder)
    {
        builder.ToTable("tenant_rate_limit_policies");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.TenantId).HasColumnName("tenant_id");
        builder.Property(e => e.Channel).HasColumnName("channel").HasMaxLength(20);
        builder.Property(e => e.Status).HasColumnName("status").HasMaxLength(20).HasDefaultValue("active");
        builder.Property(e => e.MaxRequestsPerMinute).HasColumnName("max_requests_per_minute");
        builder.Property(e => e.MaxAttemptsPerMinute).HasColumnName("max_attempts_per_minute");
        builder.Property(e => e.MaxDailyUsage).HasColumnName("max_daily_usage");
        builder.Property(e => e.MaxMonthlyUsage).HasColumnName("max_monthly_usage");
        builder.Property(e => e.CreatedAt).HasColumnName("created_at");
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at");
    }
}

public class TenantContactPolicyConfiguration : IEntityTypeConfiguration<TenantContactPolicy>
{
    public void Configure(EntityTypeBuilder<TenantContactPolicy> builder)
    {
        builder.ToTable("tenant_contact_policies");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.TenantId).HasColumnName("tenant_id");
        builder.Property(e => e.Channel).HasColumnName("channel").HasMaxLength(20);
        builder.Property(e => e.BlockSuppressedContacts).HasColumnName("block_suppressed_contacts").HasDefaultValue(true);
        builder.Property(e => e.BlockUnsubscribedContacts).HasColumnName("block_unsubscribed_contacts").HasDefaultValue(true);
        builder.Property(e => e.BlockComplainedContacts).HasColumnName("block_complained_contacts").HasDefaultValue(true);
        builder.Property(e => e.BlockBouncedContacts).HasColumnName("block_bounced_contacts").HasDefaultValue(false);
        builder.Property(e => e.BlockInvalidContacts).HasColumnName("block_invalid_contacts").HasDefaultValue(false);
        builder.Property(e => e.BlockCarrierRejectedContacts).HasColumnName("block_carrier_rejected_contacts").HasDefaultValue(false);
        builder.Property(e => e.AllowManualOverride).HasColumnName("allow_manual_override").HasDefaultValue(false);
        builder.Property(e => e.CreatedAt).HasColumnName("created_at");
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at");
    }
}

public class TenantBrandingConfiguration : IEntityTypeConfiguration<TenantBranding>
{
    public void Configure(EntityTypeBuilder<TenantBranding> builder)
    {
        builder.ToTable("tenant_brandings");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.TenantId).HasColumnName("tenant_id");
        builder.Property(e => e.ProductType).HasColumnName("product_type").HasMaxLength(50);
        builder.Property(e => e.BrandName).HasColumnName("brand_name").HasMaxLength(200);
        builder.Property(e => e.LogoUrl).HasColumnName("logo_url").HasMaxLength(2000);
        builder.Property(e => e.PrimaryColor).HasColumnName("primary_color").HasMaxLength(50);
        builder.Property(e => e.SecondaryColor).HasColumnName("secondary_color").HasMaxLength(50);
        builder.Property(e => e.AccentColor).HasColumnName("accent_color").HasMaxLength(50);
        builder.Property(e => e.TextColor).HasColumnName("text_color").HasMaxLength(50);
        builder.Property(e => e.BackgroundColor).HasColumnName("background_color").HasMaxLength(50);
        builder.Property(e => e.ButtonRadius).HasColumnName("button_radius").HasMaxLength(30);
        builder.Property(e => e.FontFamily).HasColumnName("font_family").HasMaxLength(200);
        builder.Property(e => e.SupportEmail).HasColumnName("support_email").HasMaxLength(255);
        builder.Property(e => e.SupportPhone).HasColumnName("support_phone").HasMaxLength(50);
        builder.Property(e => e.WebsiteUrl).HasColumnName("website_url").HasMaxLength(2000);
        builder.Property(e => e.EmailHeaderHtml).HasColumnName("email_header_html").HasColumnType("text");
        builder.Property(e => e.EmailFooterHtml).HasColumnName("email_footer_html").HasColumnType("text");
        builder.Property(e => e.CreatedAt).HasColumnName("created_at");
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at");

        builder.HasIndex(e => new { e.TenantId, e.ProductType })
            .HasDatabaseName("uq_tenant_branding")
            .IsUnique();
    }
}

public class UsageMeterEventConfiguration : IEntityTypeConfiguration<UsageMeterEvent>
{
    public void Configure(EntityTypeBuilder<UsageMeterEvent> builder)
    {
        builder.ToTable("usage_meter_events");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.TenantId).HasColumnName("tenant_id");
        builder.Property(e => e.NotificationId).HasColumnName("notification_id");
        builder.Property(e => e.NotificationAttemptId).HasColumnName("notification_attempt_id");
        builder.Property(e => e.Channel).HasColumnName("channel").HasMaxLength(20);
        builder.Property(e => e.Provider).HasColumnName("provider").HasMaxLength(100);
        builder.Property(e => e.ProviderOwnershipMode).HasColumnName("provider_ownership_mode").HasMaxLength(20);
        builder.Property(e => e.ProviderConfigId).HasColumnName("provider_config_id");
        builder.Property(e => e.UsageUnit).HasColumnName("usage_unit").HasMaxLength(100);
        builder.Property(e => e.Quantity).HasColumnName("quantity").HasDefaultValue(1);
        builder.Property(e => e.IsBillable).HasColumnName("is_billable");
        builder.Property(e => e.ProviderUnitCost).HasColumnName("provider_unit_cost").HasColumnType("decimal(10,6)");
        builder.Property(e => e.ProviderTotalCost).HasColumnName("provider_total_cost").HasColumnType("decimal(10,6)");
        builder.Property(e => e.Currency).HasColumnName("currency").HasMaxLength(10);
        builder.Property(e => e.MetadataJson).HasColumnName("metadata_json").HasColumnType("text");
        builder.Property(e => e.OccurredAt).HasColumnName("occurred_at");
        builder.Property(e => e.CreatedAt).HasColumnName("created_at");
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at");

        builder.HasIndex(e => new { e.TenantId, e.UsageUnit, e.OccurredAt })
            .HasDatabaseName("idx_usage_meter_events_tenant_unit_time");
    }
}
