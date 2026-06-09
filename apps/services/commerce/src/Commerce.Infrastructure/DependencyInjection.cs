using Commerce.Application.AccountStanding.Abstractions;
using Commerce.Application.Admin.Abstractions;
using Commerce.Application.Billing.Abstractions;
using Commerce.Application.Catalog.Abstractions;
using Commerce.Application.Integration.Abstractions;
using Commerce.Application.Invoicing.Abstractions;
using Commerce.Application.Payments.Abstractions;
using Commerce.Application.Subscriptions.Abstractions;
using Commerce.Domain.AccountStanding;
using Commerce.Infrastructure.AccountStanding.Services;
using Commerce.Infrastructure.Admin.Services;
using Commerce.Infrastructure.Billing.Services;
using Commerce.Infrastructure.Catalog.Services;
using Commerce.Infrastructure.Integration.HostAdapters;
using Commerce.Infrastructure.Integration.Services;
using Commerce.Infrastructure.Integration.TenantBilling;
using Commerce.Infrastructure.Invoicing.Services;
using Commerce.Infrastructure.Payments;
using Commerce.Infrastructure.Payments.Configuration;
using Commerce.Infrastructure.Payments.Services;
using Commerce.Infrastructure.Payments.Stripe;
using Commerce.Infrastructure.Subscriptions.Services;
using Commerce.Infrastructure.Persistence;
using Commerce.Infrastructure.Resilience;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Commerce.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddCommerceInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration["Database:ConnectionString"];

        services.AddDbContext<CommerceDbContext>(options =>
        {
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                // No DB configured: register a no-op options object so the host still starts.
                // /ready will report database: not-configured. Callers must not perform queries.
                options.UseInMemoryDatabaseFallback();
                return;
            }

            options.UseMySql(
                connectionString,
                ServerVersion.AutoDetect(connectionString),
                mysql => mysql.MigrationsAssembly(typeof(CommerceDbContext).Assembly.FullName));
        });

        services.AddSingleton<IResiliencePolicyProvider, ResiliencePolicyProvider>();

        // Catalog (COM-B02)
        services.AddScoped<IProductCatalogService, ProductCatalogService>();
        services.AddScoped<IFeatureCatalogService, FeatureCatalogService>();
        services.AddScoped<IPlanCatalogService, PlanCatalogService>();
        services.AddScoped<IAddonCatalogService, AddonCatalogService>();
        services.AddScoped<IBundleCatalogService, BundleCatalogService>();
        services.AddScoped<IPriceCatalogService, PriceCatalogService>();

        // Billing (COM-B03)
        services.AddScoped<IAccountNumberGenerator, AccountNumberGenerator>();
        services.AddScoped<BillingAuditWriter>();
        services.AddScoped<IBillingAccountService, BillingAccountService>();
        services.AddScoped<IBillingAccountExternalRefService, BillingAccountExternalRefService>();
        services.AddScoped<IBillingContactService, BillingContactService>();
        services.AddScoped<IBillingProfileService, BillingProfileService>();
        services.AddScoped<IBillingAccountAuditService, BillingAccountAuditService>();

        // Subscriptions (COM-B04)
        services.AddScoped<ISubscriptionNumberGenerator, SubscriptionNumberGenerator>();
        services.AddScoped<SubscriptionChangeWriter>();
        services.AddScoped<ISubscriptionService, SubscriptionService>();

        // Payments (COM-B05)
        services.Configure<PaymentProvidersOptions>(
            configuration.GetSection(PaymentProvidersOptions.SectionName));
        services.AddHttpClient<StripePaymentProvider>();
        services.AddScoped<IPaymentProvider>(sp => sp.GetRequiredService<StripePaymentProvider>());
        services.AddScoped<IPaymentProviderRegistry, PaymentProviderRegistry>();
        services.AddScoped<IPaymentProviderCustomerService, PaymentProviderCustomerService>();
        services.AddScoped<IPaymentCheckoutService, PaymentCheckoutService>();
        services.AddScoped<IPaymentWebhookService, PaymentWebhookService>();
        services.AddScoped<IPaymentMethodReferenceService, PaymentMethodReferenceService>();

        // Invoicing, Account Standing & Reconciliation (COM-B06)
        services.AddValidatorsFromAssembly(typeof(Commerce.Application.AssemblyMarker).Assembly);
        services.AddSingleton(AccountStandingPolicy.Default);
        services.AddScoped<IInvoiceNumberGenerator, InvoiceNumberGenerator>();
        services.AddScoped<IInvoiceService, InvoiceService>();
        services.AddScoped<IInvoiceBrandingService, InvoiceBrandingService>();
        services.AddScoped<IPaymentRecordService, PaymentRecordQueryService>();
        services.AddScoped<IPaymentRecordingService, PaymentRecordingService>();
        services.AddScoped<IManualPaymentRecordingService, ManualPaymentRecordingService>();
        services.AddScoped<ISubscriptionReconciliationService, SubscriptionReconciliationService>();
        services.AddScoped<IProviderEventReplayService, ProviderEventReplayService>();
        services.AddScoped<IAccountStandingService, AccountStandingService>();

        // Admin & Operability (COM-B07)
        services.AddScoped<IAdminDashboardService, AdminDashboardService>();

        // Host Integration Contracts (COM-B08)
        // Defaults are intentionally local/no-op so Commerce runs standalone.
        // A future host integration phase will replace these registrations.
        services.AddSingleton<IHostIdentityContextAccessor, LocalHostIdentityContextAccessor>();
        services.AddScoped<IHostTenantResolver, NoopHostTenantResolver>();
        services.AddSingleton<IProvisioningHookPublisher, NoopProvisioningHookPublisher>();
        services.AddScoped<IHostIntegrationAdapter, LocalHostIntegrationAdapter>();
        services.AddScoped<ICommerceAccessRecommendationService, CommerceAccessRecommendationService>();
        services.AddScoped<ICommerceEntitlementSnapshotService, CommerceEntitlementSnapshotService>();

        // Tenant Billing entitlement publisher (TB-INT-01 / TB-INT-02).
        // Disabled by default; the typed HttpClient registration is a
        // no-op until Commerce:TenantBilling:Enabled is set true.
        // Circuit breaker and metrics are singletons so state and
        // counters survive across the typed client's transient
        // HttpClient instances.
        services.Configure<TenantBillingClientOptions>(
            configuration.GetSection(TenantBillingClientOptions.SectionName));
        services.AddSingleton<ITenantBillingPublisherCircuitBreaker,
            TenantBillingPublisherCircuitBreaker>();
        services.AddSingleton<TenantBillingPublisherMetrics>();
        services.AddHttpClient<ITenantBillingEntitlementPublisher, TenantBillingEntitlementPublisher>();

        // TB-INT-03 — auto-publish queue + hosted worker. Always
        // registered; the queue refuses writes when AutoPublishEnabled
        // is false so toggling the config is sufficient to enable or
        // disable the feature without redeploying. Singleton queue;
        // worker resolves the publisher in a fresh scope per item.
        services.AddSingleton<BoundedTenantBillingEntitlementPublishQueue>();
        services.AddSingleton<ITenantBillingEntitlementPublishQueue>(
            sp => sp.GetRequiredService<BoundedTenantBillingEntitlementPublishQueue>());
        services.AddHostedService<TenantBillingEntitlementPublishWorker>();

        // TB-INT-04 — durable Commerce → Tenant Billing entitlement
        // publish outbox. Always registered; the worker no-ops while
        // Commerce:TenantBilling:OutboxEnabled is false so toggling
        // the config is sufficient to enable the durable path
        // without redeploying.
        services.AddScoped<ITenantBillingEntitlementOutbox,
            Commerce.Infrastructure.Integration.TenantBilling.Outbox.EfTenantBillingEntitlementOutbox>();
        services.AddScoped<ITenantBillingEntitlementOutboxProcessor,
            Commerce.Infrastructure.Integration.TenantBilling.Outbox.TenantBillingEntitlementOutboxProcessor>();
        services.AddHostedService<
            Commerce.Infrastructure.Integration.TenantBilling.Outbox.TenantBillingEntitlementOutboxWorker>();

        return services;
    }
}

internal static class DbContextOptionsBuilderExtensions
{
    /// <summary>
    /// Local-development fallback when no MySQL connection string is configured.
    /// Uses InMemory provider so the app boots; readiness still reports "not-configured".
    /// </summary>
    public static DbContextOptionsBuilder UseInMemoryDatabaseFallback(this DbContextOptionsBuilder builder)
    {
        return builder.UseInMemoryDatabase("commerce-fallback");
    }
}
