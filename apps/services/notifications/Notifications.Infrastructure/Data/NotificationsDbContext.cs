using Microsoft.EntityFrameworkCore;
using Notifications.Domain;

namespace Notifications.Infrastructure.Data;

public class NotificationsDbContext : DbContext
{
    public NotificationsDbContext(DbContextOptions<NotificationsDbContext> options)
        : base(options) { }

    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<NotificationAttempt> NotificationAttempts => Set<NotificationAttempt>();
    public DbSet<Template> Templates => Set<Template>();
    public DbSet<TemplateVersion> TemplateVersions => Set<TemplateVersion>();
    public DbSet<TenantProviderConfig> TenantProviderConfigs => Set<TenantProviderConfig>();
    public DbSet<TenantChannelProviderSetting> TenantChannelProviderSettings => Set<TenantChannelProviderSetting>();
    public DbSet<ProviderHealth> ProviderHealthRecords => Set<ProviderHealth>();
    public DbSet<ProviderWebhookLog> ProviderWebhookLogs => Set<ProviderWebhookLog>();
    public DbSet<NotificationEvent> NotificationEvents => Set<NotificationEvent>();
    public DbSet<RecipientContactHealth> RecipientContactHealthRecords => Set<RecipientContactHealth>();
    public DbSet<DeliveryIssue> DeliveryIssues => Set<DeliveryIssue>();
    public DbSet<ContactSuppression> ContactSuppressions => Set<ContactSuppression>();
    public DbSet<TenantBillingPlan> TenantBillingPlans => Set<TenantBillingPlan>();
    public DbSet<TenantBillingRate> TenantBillingRates => Set<TenantBillingRate>();
    public DbSet<TenantRateLimitPolicy> TenantRateLimitPolicies => Set<TenantRateLimitPolicy>();
    public DbSet<TenantContactPolicy> TenantContactPolicies => Set<TenantContactPolicy>();
    public DbSet<TenantBranding> TenantBrandings => Set<TenantBranding>();
    public DbSet<UsageMeterEvent> UsageMeterEvents => Set<UsageMeterEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfiguration(new Configurations.NotificationConfiguration());
        modelBuilder.ApplyConfiguration(new Configurations.NotificationAttemptConfiguration());
        modelBuilder.ApplyConfiguration(new Configurations.TemplateConfiguration());
        modelBuilder.ApplyConfiguration(new Configurations.TemplateVersionConfiguration());
        modelBuilder.ApplyConfiguration(new Configurations.TenantProviderConfigConfiguration());
        modelBuilder.ApplyConfiguration(new Configurations.TenantChannelProviderSettingConfiguration());
        modelBuilder.ApplyConfiguration(new Configurations.ProviderHealthConfiguration());
        modelBuilder.ApplyConfiguration(new Configurations.ProviderWebhookLogConfiguration());
        modelBuilder.ApplyConfiguration(new Configurations.NotificationEventConfiguration());
        modelBuilder.ApplyConfiguration(new Configurations.RecipientContactHealthConfiguration());
        modelBuilder.ApplyConfiguration(new Configurations.DeliveryIssueConfiguration());
        modelBuilder.ApplyConfiguration(new Configurations.ContactSuppressionConfiguration());
        modelBuilder.ApplyConfiguration(new Configurations.TenantBillingPlanConfiguration());
        modelBuilder.ApplyConfiguration(new Configurations.TenantBillingRateConfiguration());
        modelBuilder.ApplyConfiguration(new Configurations.TenantRateLimitPolicyConfiguration());
        modelBuilder.ApplyConfiguration(new Configurations.TenantContactPolicyConfiguration());
        modelBuilder.ApplyConfiguration(new Configurations.TenantBrandingConfiguration());
        modelBuilder.ApplyConfiguration(new Configurations.UsageMeterEventConfiguration());
    }
}
