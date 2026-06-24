using AccountStandingEntity = Commerce.Domain.AccountStanding.AccountStanding;
using Commerce.Domain.Billing;
using Commerce.Domain.Catalog;
using Commerce.Domain.Infrastructure;
using Commerce.Domain.Invoicing;
using Commerce.Domain.Payments;
using Commerce.Domain.Subscriptions;
using Commerce.Infrastructure.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;

namespace Commerce.Infrastructure.Persistence;

public class CommerceDbContext : DbContext
{
    public CommerceDbContext(DbContextOptions<CommerceDbContext> options) : base(options) { }

    public DbSet<CommerceSchemaMarker> SchemaMarkers => Set<CommerceSchemaMarker>();

    public DbSet<Product> Products => Set<Product>();
    public DbSet<Feature> Features => Set<Feature>();
    public DbSet<Plan> Plans => Set<Plan>();
    public DbSet<PlanFeature> PlanFeatures => Set<PlanFeature>();
    public DbSet<Addon> Addons => Set<Addon>();
    public DbSet<Bundle> Bundles => Set<Bundle>();
    public DbSet<BundleItem> BundleItems => Set<BundleItem>();
    public DbSet<Price> Prices => Set<Price>();

    public DbSet<BillingAccount> BillingAccounts => Set<BillingAccount>();
    public DbSet<BillingAccountExternalRef> BillingAccountExternalRefs => Set<BillingAccountExternalRef>();
    public DbSet<BillingContact> BillingContacts => Set<BillingContact>();
    public DbSet<BillingProfile> BillingProfiles => Set<BillingProfile>();
    public DbSet<BillingAccountAuditEvent> BillingAccountAuditEvents => Set<BillingAccountAuditEvent>();

    public DbSet<Subscription> Subscriptions => Set<Subscription>();
    public DbSet<SubscriptionItem> SubscriptionItems => Set<SubscriptionItem>();
    public DbSet<SubscriptionChange> SubscriptionChanges => Set<SubscriptionChange>();

    public DbSet<PaymentProviderCustomer> PaymentProviderCustomers => Set<PaymentProviderCustomer>();
    public DbSet<PaymentProviderSubscription> PaymentProviderSubscriptions => Set<PaymentProviderSubscription>();
    public DbSet<PaymentMethodReference> PaymentMethodReferences => Set<PaymentMethodReference>();
    public DbSet<PaymentProviderEventLog> PaymentProviderEventLogs => Set<PaymentProviderEventLog>();

    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<InvoiceLine> InvoiceLines => Set<InvoiceLine>();
    public DbSet<InvoiceBranding> InvoiceBrandings => Set<InvoiceBranding>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<PaymentAttempt> PaymentAttempts => Set<PaymentAttempt>();
    public DbSet<AccountStandingEntity> AccountStandings => Set<AccountStandingEntity>();

    // TB-INT-04 — durable Commerce → Tenant Billing entitlement publish outbox.
    public DbSet<Commerce.Infrastructure.Integration.TenantBilling.Outbox.TenantBillingEntitlementPublishOutboxRow> TenantBillingEntitlementPublishOutbox
        => Set<Commerce.Infrastructure.Integration.TenantBilling.Outbox.TenantBillingEntitlementPublishOutboxRow>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfiguration(new CommerceSchemaMarkerConfiguration());

        modelBuilder.ApplyConfiguration(new ProductConfiguration());
        modelBuilder.ApplyConfiguration(new FeatureConfiguration());
        modelBuilder.ApplyConfiguration(new PlanConfiguration());
        modelBuilder.ApplyConfiguration(new PlanFeatureConfiguration());
        modelBuilder.ApplyConfiguration(new AddonConfiguration());
        modelBuilder.ApplyConfiguration(new BundleConfiguration());
        modelBuilder.ApplyConfiguration(new BundleItemConfiguration());
        modelBuilder.ApplyConfiguration(new PriceConfiguration());

        modelBuilder.ApplyConfiguration(new BillingAccountConfiguration());
        modelBuilder.ApplyConfiguration(new BillingAccountExternalRefConfiguration());
        modelBuilder.ApplyConfiguration(new BillingContactConfiguration());
        modelBuilder.ApplyConfiguration(new BillingProfileConfiguration());
        modelBuilder.ApplyConfiguration(new BillingAccountAuditEventConfiguration());

        modelBuilder.ApplyConfiguration(new SubscriptionConfiguration());
        modelBuilder.ApplyConfiguration(new SubscriptionItemConfiguration());
        modelBuilder.ApplyConfiguration(new SubscriptionChangeConfiguration());

        modelBuilder.ApplyConfiguration(new PaymentProviderCustomerConfiguration());
        modelBuilder.ApplyConfiguration(new PaymentProviderSubscriptionConfiguration());
        modelBuilder.ApplyConfiguration(new PaymentMethodReferenceConfiguration());
        modelBuilder.ApplyConfiguration(new PaymentProviderEventLogConfiguration());

        modelBuilder.ApplyConfiguration(new InvoiceConfiguration());
        modelBuilder.ApplyConfiguration(new InvoiceLineConfiguration());
        modelBuilder.ApplyConfiguration(new InvoiceBrandingConfiguration());
        // TB-INT-04 — durable Commerce → Tenant Billing entitlement publish outbox.
        modelBuilder.ApplyConfiguration(
            new Commerce.Infrastructure.Integration.TenantBilling.Outbox
                .TenantBillingEntitlementPublishOutboxRowConfiguration());
        modelBuilder.ApplyConfiguration(new PaymentConfiguration());
        modelBuilder.ApplyConfiguration(new PaymentAttemptConfiguration());
        modelBuilder.ApplyConfiguration(new AccountStandingConfiguration());
    }
}
