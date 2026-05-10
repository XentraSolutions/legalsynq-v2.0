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
    public DbSet<SmsContactPreference> SmsContactPreferences => Set<SmsContactPreference>();
    public DbSet<SmsPreferenceHistory> SmsPreferenceHistories => Set<SmsPreferenceHistory>();
    public DbSet<SmsOperationalAlert> SmsOperationalAlerts => Set<SmsOperationalAlert>();
    public DbSet<SmsOperationalEscalationPolicy> SmsEscalationPolicies => Set<SmsOperationalEscalationPolicy>();
    public DbSet<SmsOperationalAlertEscalation> SmsAlertEscalations => Set<SmsOperationalAlertEscalation>();

    // LS-NOTIF-SMS-014: Multi-Provider SMS Routing
    public DbSet<SmsRoutingPolicy> SmsRoutingPolicies => Set<SmsRoutingPolicy>();
    public DbSet<SmsRoutingDecision> SmsRoutingDecisions => Set<SmsRoutingDecision>();

    // LS-NOTIF-SMS-015: Provider Quality Snapshots
    public DbSet<SmsProviderQualitySnapshot> SmsProviderQualitySnapshots => Set<SmsProviderQualitySnapshot>();

    // LS-NOTIF-SMS-016: Recipient Intelligence + Suppression
    public DbSet<SmsRecipientReputationSnapshot> SmsRecipientReputationSnapshots => Set<SmsRecipientReputationSnapshot>();
    public DbSet<SmsSuppressionDecision> SmsSuppressionDecisions => Set<SmsSuppressionDecision>();

    // LS-NOTIF-SMS-017: Governance Policies + Decisions
    public DbSet<SmsGovernancePolicy>   SmsGovernancePolicies  => Set<SmsGovernancePolicy>();
    public DbSet<SmsGovernanceDecision> SmsGovernanceDecisions => Set<SmsGovernanceDecision>();

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
        modelBuilder.ApplyConfiguration(new Configurations.SmsContactPreferenceConfiguration());
        modelBuilder.ApplyConfiguration(new Configurations.SmsPreferenceHistoryConfiguration());
        modelBuilder.ApplyConfiguration(new Configurations.SmsOperationalAlertConfiguration());
        modelBuilder.ApplyConfiguration(new Configurations.SmsEscalationPolicyConfiguration());
        modelBuilder.ApplyConfiguration(new Configurations.SmsAlertEscalationConfiguration());
        // LS-NOTIF-SMS-014
        modelBuilder.ApplyConfiguration(new Configurations.SmsRoutingPolicyConfiguration());
        modelBuilder.ApplyConfiguration(new Configurations.SmsRoutingDecisionConfiguration());
        // LS-NOTIF-SMS-015
        modelBuilder.ApplyConfiguration(new Configurations.SmsProviderQualitySnapshotConfiguration());
        // LS-NOTIF-SMS-016
        modelBuilder.ApplyConfiguration(new Configurations.SmsRecipientReputationSnapshotConfiguration());
        modelBuilder.ApplyConfiguration(new Configurations.SmsSuppressionDecisionConfiguration());
        // LS-NOTIF-SMS-017
        modelBuilder.ApplyConfiguration(new Configurations.SmsGovernancePolicyConfiguration());
        modelBuilder.ApplyConfiguration(new Configurations.SmsGovernanceDecisionConfiguration());
    }
}
