using Commerce.Application.Common.Time;
using Commerce.Domain.AccountStanding.Enums;
using Commerce.Domain.Billing;
using Commerce.Domain.Billing.Enums;
using Commerce.Domain.Catalog;
using Commerce.Domain.Catalog.Enums;
using Commerce.Domain.Invoicing;
using Commerce.Domain.Invoicing.Enums;
using Commerce.Domain.Payments;
using Commerce.Domain.Payments.Enums;
using Commerce.Domain.Subscriptions;
using Commerce.Infrastructure.Persistence;
using AccountStandingEntity = Commerce.Domain.AccountStanding.AccountStanding;

namespace Commerce.Api.Demo;

/// <summary>
/// Hosted service that seeds a representative dataset across every Commerce
/// section (catalog, billing, subscriptions, invoices, payments, provider
/// events, account standing) when the <c>SEED_DEMO_DATA</c> environment
/// variable is truthy. Idempotent: skips seeding if any catalog product
/// already exists. Intended for the in-memory fallback DB used by the
/// preview environment; safe but unused in production.
/// </summary>
public sealed class DemoDataSeeder : IHostedService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<DemoDataSeeder> _logger;

    public DemoDataSeeder(IServiceProvider services, ILogger<DemoDataSeeder> logger)
    {
        _services = services;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = _services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CommerceDbContext>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();

        if (db.Products.Any())
        {
            _logger.LogInformation("Demo data already present — skipping seeder.");
            return;
        }

        var now = clock.UtcNow;
        _logger.LogInformation("Seeding Commerce demo dataset @ {Now}", now);

        SeedCatalog(db, now,
            out var planStarter, out var planPro, out var planEnterprise, out var planAnalytics,
            out var priceStarter, out var pricePro, out var priceEnterprise, out var priceAnalytics);
        SeedBillingAccountsAndStandings(db, now,
            out var acmeAccount, out var globexAccount, out var initechAccount,
            out var umbrellaAccount, out var soylentAccount);
        await db.SaveChangesAsync(cancellationToken);

        SeedSubscriptions(db, now,
            acmeAccount, globexAccount, initechAccount, umbrellaAccount,
            planPro, planEnterprise, planStarter, planAnalytics,
            pricePro, priceEnterprise, priceStarter, priceAnalytics,
            out var acmeSub, out var globexSub, out var initechSub);
        await db.SaveChangesAsync(cancellationToken);

        SeedInvoices(db, now,
            acmeAccount, globexAccount, initechAccount, umbrellaAccount, soylentAccount,
            acmeSub, globexSub, initechSub);
        SeedPayments(db, now, acmeAccount, globexAccount, initechAccount);
        SeedProviderEvents(db, now);
        await db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Commerce demo dataset seeded.");
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    // ----------------------------------------------------------------- Catalog

    private static void SeedCatalog(
        CommerceDbContext db,
        DateTime now,
        out Plan planStarter,
        out Plan planPro,
        out Plan planEnterprise,
        out Plan planAnalytics,
        out Price priceStarter,
        out Price pricePro,
        out Price priceEnterprise,
        out Price priceAnalytics)
    {
        var pStorage = Product.Create("cloud-storage", "Cloud Storage", "Encrypted document storage with versioning.", 0, now);
        pStorage.Activate(now);
        var pAnalytics = Product.Create("analytics-suite", "Analytics Suite", "Reporting and dashboards across your firm.", 1, now);
        pAnalytics.Activate(now);
        var pLegacy = Product.Create("legacy-fax", "Legacy Fax Gateway", "Sunset product retained for migration only.", 99, now);
        pLegacy.Activate(now);
        pLegacy.Retire(now);

        // Features for storage product
        var fStorageQuota = Feature.Create(pStorage.Id, "storage-quota-gb", "Storage Quota", "Total GB available", FeatureType.Limit, now);
        fStorageQuota.Activate(now);
        var fStorageVersioning = Feature.Create(pStorage.Id, "version-history", "Version History", "Per-document version retention", FeatureType.Boolean, now);
        fStorageVersioning.Activate(now);
        var fStorageSso = Feature.Create(pStorage.Id, "sso-saml", "SSO (SAML)", "SAML single sign-on", FeatureType.Boolean, now);
        fStorageSso.Activate(now);

        // Features for analytics product
        var fAnalyticsSeats = Feature.Create(pAnalytics.Id, "analyst-seats", "Analyst Seats", "Concurrent analyst seats", FeatureType.Limit, now);
        fAnalyticsSeats.Activate(now);
        var fAnalyticsApi = Feature.Create(pAnalytics.Id, "api-access", "API Access", "Programmatic export API", FeatureType.Boolean, now);
        fAnalyticsApi.Activate(now);

        // Plans
        planStarter = Plan.Create(pStorage.Id, "storage-starter", "Storage Starter", "10 GB, single user.", BillingInterval.Monthly, null, 0, now);
        planStarter.Activate(now);
        planPro = Plan.Create(pStorage.Id, "storage-pro", "Storage Pro", "100 GB, version history.", BillingInterval.Monthly, null, 1, now);
        planPro.Activate(now);
        planEnterprise = Plan.Create(pStorage.Id, "storage-enterprise", "Storage Enterprise", "1 TB, SSO, audit logs.", BillingInterval.Annual, null, 2, now);
        planEnterprise.Activate(now);
        planAnalytics = Plan.Create(pAnalytics.Id, "analytics-standard", "Analytics Standard", "5 analyst seats, dashboards.", BillingInterval.Monthly, null, 0, now);
        planAnalytics.Activate(now);

        var planRetired = Plan.Create(pStorage.Id, "storage-legacy", "Storage Legacy", "Deprecated tier, do not sell.", BillingInterval.Monthly, null, 99, now);
        planRetired.Activate(now);
        planRetired.Retire(now);

        // PlanFeatures
        var pf1 = PlanFeature.Create(planStarter.Id, fStorageQuota.Id, isEnabled: true, limitValue: 10, meteredIncludedUnits: null, now);
        var pf2 = PlanFeature.Create(planPro.Id, fStorageQuota.Id, isEnabled: true, limitValue: 100, meteredIncludedUnits: null, now);
        var pf3 = PlanFeature.Create(planPro.Id, fStorageVersioning.Id, isEnabled: true, limitValue: null, meteredIncludedUnits: null, now);
        var pf4 = PlanFeature.Create(planEnterprise.Id, fStorageQuota.Id, isEnabled: true, limitValue: 1024, meteredIncludedUnits: null, now);
        var pf5 = PlanFeature.Create(planEnterprise.Id, fStorageVersioning.Id, isEnabled: true, limitValue: null, meteredIncludedUnits: null, now);
        var pf6 = PlanFeature.Create(planEnterprise.Id, fStorageSso.Id, isEnabled: true, limitValue: null, meteredIncludedUnits: null, now);
        var pf7 = PlanFeature.Create(planAnalytics.Id, fAnalyticsSeats.Id, isEnabled: true, limitValue: 5, meteredIncludedUnits: null, now);
        var pf8 = PlanFeature.Create(planAnalytics.Id, fAnalyticsApi.Id, isEnabled: true, limitValue: null, meteredIncludedUnits: null, now);

        // Add-ons
        var addonExtraStorage = Addon.Create(pStorage.Id, "extra-storage-100gb", "+100 GB Storage", "Add 100 GB to any plan.", now);
        addonExtraStorage.Activate(now);
        var addonAuditExports = Addon.Create(pAnalytics.Id, "audit-exports", "Audit Log Exports", "CSV exports of all audit events.", now);
        addonAuditExports.Activate(now);
        var addonPriority = Addon.Create(null, "priority-support", "Priority Support", "24x7 named CSM.", now);
        addonPriority.Activate(now);

        // Bundles + items
        var bundleProductivity = Bundle.Create("productivity-pack", "Productivity Pack", "Storage Pro + Analytics Standard.", now);
        bundleProductivity.Activate(now);
        var biProd1 = BundleItem.Create(bundleProductivity.Id, productId: null, planId: planPro.Id, addonId: null, now);
        var biProd2 = BundleItem.Create(bundleProductivity.Id, productId: null, planId: planAnalytics.Id, addonId: null, now);

        var bundleEnterprise = Bundle.Create("enterprise-bundle", "Enterprise Bundle", "Storage Enterprise + Priority Support.", now);
        bundleEnterprise.Activate(now);
        var biEnt1 = BundleItem.Create(bundleEnterprise.Id, productId: null, planId: planEnterprise.Id, addonId: null, now);
        var biEnt2 = BundleItem.Create(bundleEnterprise.Id, productId: null, planId: null, addonId: addonPriority.Id, now);

        // Prices
        priceStarter = Price.Create(planStarter.Id, null, null, "USD", 999, BillingInterval.Monthly, now.AddDays(-30), null, now);
        priceStarter.Activate(now);
        pricePro = Price.Create(planPro.Id, null, null, "USD", 2499, BillingInterval.Monthly, now.AddDays(-30), null, now);
        pricePro.Activate(now);
        var priceProYearly = Price.Create(planPro.Id, null, null, "USD", 24990, BillingInterval.Annual, now.AddDays(-30), null, now);
        priceProYearly.Activate(now);
        priceEnterprise = Price.Create(planEnterprise.Id, null, null, "USD", 99900, BillingInterval.Annual, now.AddDays(-30), null, now);
        priceEnterprise.Activate(now);
        priceAnalytics = Price.Create(planAnalytics.Id, null, null, "USD", 4999, BillingInterval.Monthly, now.AddDays(-30), null, now);
        priceAnalytics.Activate(now);
        var priceAddonStorage = Price.Create(null, addonExtraStorage.Id, null, "USD", 500, BillingInterval.Monthly, now.AddDays(-30), null, now);
        priceAddonStorage.Activate(now);

        db.AddRange(pStorage, pAnalytics, pLegacy);
        db.AddRange(fStorageQuota, fStorageVersioning, fStorageSso, fAnalyticsSeats, fAnalyticsApi);
        db.AddRange(planStarter, planPro, planEnterprise, planAnalytics, planRetired);
        db.AddRange(pf1, pf2, pf3, pf4, pf5, pf6, pf7, pf8);
        db.AddRange(addonExtraStorage, addonAuditExports, addonPriority);
        db.AddRange(bundleProductivity, bundleEnterprise);
        db.AddRange(biProd1, biProd2, biEnt1, biEnt2);
        db.AddRange(priceStarter, pricePro, priceProYearly, priceEnterprise, priceAnalytics, priceAddonStorage);
    }

    // ---------------------------------------------------- Billing + Standings

    private static void SeedBillingAccountsAndStandings(
        CommerceDbContext db,
        DateTime now,
        out BillingAccount acme,
        out BillingAccount globex,
        out BillingAccount initech,
        out BillingAccount umbrella,
        out BillingAccount soylent)
    {
        acme = BillingAccount.Create("COM-ACC-1001", "Acme Legal LLP", null, "USD", now);
        acme.Activate(now);
        var acmeProfile = BillingProfile.CreateEmpty(acme.Id, now);
        acmeProfile.Update("100 Market St", "Suite 400", "San Francisco", "CA", "94105", "US", taxId: "TAX-ACME-001", taxExempt: false, nowUtc: now);
        var acmeContact = BillingContact.Create(acme.Id, BillingContactType.Billing, "Alice Andrews", "alice@acme.example", "+1-415-555-0102", isPrimary: true, now);

        globex = BillingAccount.Create("COM-ACC-1002", "Globex Counsel Group", null, "USD", now);
        globex.Activate(now);
        var globexProfile = BillingProfile.CreateEmpty(globex.Id, now);
        globexProfile.Update("350 5th Ave", null, "New York", "NY", "10118", "US", taxId: null, taxExempt: false, nowUtc: now);
        var globexContact = BillingContact.Create(globex.Id, BillingContactType.Billing, "Greg Greene", "greg@globex.example", null, isPrimary: true, now);

        initech = BillingAccount.Create("COM-ACC-1003", "Initech Partners", null, "EUR", now);
        initech.Activate(now);
        var initechProfile = BillingProfile.CreateEmpty(initech.Id, now);
        initechProfile.Update("Friedrichstr. 1", null, "Berlin", null, "10117", "DE", taxId: "DE-12345678", taxExempt: false, nowUtc: now);
        var initechContact = BillingContact.Create(initech.Id, BillingContactType.Billing, "Inga Iverson", "inga@initech.example", null, isPrimary: true, now);

        umbrella = BillingAccount.Create("COM-ACC-1004", "Umbrella Advisory", null, "USD", now);
        umbrella.Activate(now);
        umbrella.Suspend(now);
        var umbrellaProfile = BillingProfile.CreateEmpty(umbrella.Id, now);
        umbrellaProfile.Update("1 Hive Plaza", null, "Raccoon City", "MO", "63101", "US", taxId: null, taxExempt: false, nowUtc: now);
        var umbrellaContact = BillingContact.Create(umbrella.Id, BillingContactType.Billing, "Ursula Underwood", "ursula@umbrella.example", null, isPrimary: true, now);

        soylent = BillingAccount.Create("COM-ACC-1005", "Soylent Holdings", null, "USD", now);
        soylent.Activate(now);
        soylent.Close(now);
        var soylentProfile = BillingProfile.CreateEmpty(soylent.Id, now);
        soylentProfile.Update("200 Green St", null, "Chicago", "IL", "60601", "US", taxId: null, taxExempt: false, nowUtc: now);

        db.AddRange(acme, globex, initech, umbrella, soylent);
        db.AddRange(acmeProfile, globexProfile, initechProfile, umbrellaProfile, soylentProfile);
        db.AddRange(acmeContact, globexContact, initechContact, umbrellaContact);

        // External refs (host-platform linkage)
        db.BillingAccountExternalRefs.Add(BillingAccountExternalRef.Create(
            acme.Id, "legalsynq", "tenant-acme", externalCustomerRef: "cust_acme_001", isPrimary: true, now));
        db.BillingAccountExternalRefs.Add(BillingAccountExternalRef.Create(
            globex.Id, "legalsynq", "tenant-globex", externalCustomerRef: "cust_globex_002", isPrimary: true, now));
        db.BillingAccountExternalRefs.Add(BillingAccountExternalRef.Create(
            initech.Id, "legalsynq", "tenant-initech", externalCustomerRef: "cust_initech_003", isPrimary: true, now));

        // Account standings — one per status to populate the dashboard breakdown
        var sAcme = AccountStandingEntity.Create(acme.Id, now);
        sAcme.Apply(AccountStandingStatus.Good, reason: "All invoices current.", null, null, null, now);
        var sGlobex = AccountStandingEntity.Create(globex.Id, now);
        sGlobex.Apply(AccountStandingStatus.GracePeriod, reason: "Invoice 1 day overdue.", gracePeriodEndsAtUtc: now.AddDays(6), pastDueSinceUtc: now.AddDays(-1), suspendedAtUtc: null, now);
        var sInitech = AccountStandingEntity.Create(initech.Id, now);
        sInitech.Apply(AccountStandingStatus.PastDue, reason: "Invoice past due >7 days.", null, pastDueSinceUtc: now.AddDays(-9), null, now);
        var sUmbrella = AccountStandingEntity.Create(umbrella.Id, now);
        sUmbrella.Apply(AccountStandingStatus.Suspended, reason: "Service suspended for non-payment.", null, pastDueSinceUtc: now.AddDays(-30), suspendedAtUtc: now.AddDays(-7), now);
        var sSoylent = AccountStandingEntity.Create(soylent.Id, now);
        sSoylent.Apply(AccountStandingStatus.Closed, reason: "Account closed by request.", null, null, null, now);
        db.AddRange(sAcme, sGlobex, sInitech, sUmbrella, sSoylent);
    }

    // ---------------------------------------------------------- Subscriptions

    private static void SeedSubscriptions(
        CommerceDbContext db,
        DateTime now,
        BillingAccount acme, BillingAccount globex, BillingAccount initech, BillingAccount umbrella,
        Plan planPro, Plan planEnterprise, Plan planStarter, Plan planAnalytics,
        Price pricePro, Price priceEnterprise, Price priceStarter, Price priceAnalytics,
        out Subscription acmeSub, out Subscription globexSub, out Subscription initechSub)
    {
        // Acme — active monthly Pro
        acmeSub = Subscription.Create(acme.Id, "COM-SUB-1001",
            startDateUtc: now.AddDays(-90),
            currentPeriodStartUtc: now.AddDays(-10),
            currentPeriodEndUtc: now.AddDays(20),
            trialStartUtc: null, trialEndUtc: null, nowUtc: now);
        var acmeItem = SubscriptionItem.Create(acmeSub.Id, planPro.Id, pricePro.Id, quantity: 1, unitAmountMinor: 2499, currency: "USD", interval: BillingInterval.Monthly, effectiveFromUtc: now.AddDays(-90), nowUtc: now);
        db.Subscriptions.Add(acmeSub);
        db.SubscriptionItems.Add(acmeItem);

        // Globex — trialing on Enterprise yearly
        globexSub = Subscription.Create(globex.Id, "COM-SUB-1002",
            startDateUtc: now.AddDays(-3),
            currentPeriodStartUtc: now.AddDays(-3),
            currentPeriodEndUtc: now.AddDays(11),
            trialStartUtc: now.AddDays(-3), trialEndUtc: now.AddDays(11), nowUtc: now);
        var globexItem = SubscriptionItem.Create(globexSub.Id, planEnterprise.Id, priceEnterprise.Id, quantity: 1, unitAmountMinor: 99900, currency: "USD", interval: BillingInterval.Annual, effectiveFromUtc: now.AddDays(-3), nowUtc: now);
        db.Subscriptions.Add(globexSub);
        db.SubscriptionItems.Add(globexItem);

        // Initech — active monthly Analytics, qty 3
        initechSub = Subscription.Create(initech.Id, "COM-SUB-1003",
            startDateUtc: now.AddDays(-180),
            currentPeriodStartUtc: now.AddDays(-5),
            currentPeriodEndUtc: now.AddDays(25),
            trialStartUtc: null, trialEndUtc: null, nowUtc: now);
        var initechItem = SubscriptionItem.Create(initechSub.Id, planAnalytics.Id, priceAnalytics.Id, quantity: 3, unitAmountMinor: 4999, currency: "EUR", interval: BillingInterval.Monthly, effectiveFromUtc: now.AddDays(-180), nowUtc: now);
        db.Subscriptions.Add(initechSub);
        db.SubscriptionItems.Add(initechItem);

        // Umbrella — suspended (created Active, then suspended)
        var umbrellaSub = Subscription.Create(umbrella.Id, "COM-SUB-1004",
            startDateUtc: now.AddDays(-200),
            currentPeriodStartUtc: now.AddDays(-12),
            currentPeriodEndUtc: now.AddDays(18),
            trialStartUtc: null, trialEndUtc: null, nowUtc: now);
        var umbrellaItem = SubscriptionItem.Create(umbrellaSub.Id, planPro.Id, pricePro.Id, quantity: 1, unitAmountMinor: 2499, currency: "USD", interval: BillingInterval.Monthly, effectiveFromUtc: now.AddDays(-200), nowUtc: now);
        umbrellaSub.Suspend(now);
        db.Subscriptions.Add(umbrellaSub);
        db.SubscriptionItems.Add(umbrellaItem);

        // Cancelled history sub on Acme (so Cancelled bucket has data)
        var acmeCancelled = Subscription.Create(acme.Id, "COM-SUB-1005",
            startDateUtc: now.AddDays(-365),
            currentPeriodStartUtc: now.AddDays(-30),
            currentPeriodEndUtc: now.AddDays(-1),
            trialStartUtc: null, trialEndUtc: null, nowUtc: now);
        acmeCancelled.Cancel(cancelAtPeriodEnd: false, reason: "Customer downgraded.", now);
        var acmeCancelledItem = SubscriptionItem.Create(acmeCancelled.Id, planStarter.Id, priceStarter.Id, quantity: 1, unitAmountMinor: 999, currency: "USD", interval: BillingInterval.Monthly, effectiveFromUtc: now.AddDays(-365), nowUtc: now);
        db.Subscriptions.Add(acmeCancelled);
        db.SubscriptionItems.Add(acmeCancelledItem);
    }

    // --------------------------------------------------------------- Invoices

    private static void SeedInvoices(
        CommerceDbContext db,
        DateTime now,
        BillingAccount acme, BillingAccount globex, BillingAccount initech, BillingAccount umbrella, BillingAccount soylent,
        Subscription acmeSub, Subscription globexSub, Subscription initechSub)
    {
        // Acme — paid invoice (recent)
        AddInvoice(db, now, acme.Id, acmeSub.Id, "COM-INV-2001", "USD", 2499, InvoiceStatus.Open, payInFull: true, daysAgo: 25);
        // Acme — paid invoice (older)
        AddInvoice(db, now, acme.Id, acmeSub.Id, "COM-INV-2002", "USD", 2499, InvoiceStatus.Open, payInFull: true, daysAgo: 55);
        // Globex — open (just issued, trialing)
        AddInvoice(db, now, globex.Id, globexSub.Id, "COM-INV-2003", "USD", 99900, InvoiceStatus.Open, payInFull: false, daysAgo: 0);
        // Initech — open partial-pay
        AddInvoice(db, now, initech.Id, initechSub.Id, "COM-INV-2004", "EUR", 14997, InvoiceStatus.Open, payInFull: false, daysAgo: 9, partialPayMinor: 5000);
        // Umbrella — open & overdue
        AddInvoice(db, now, umbrella.Id, null, "COM-INV-2005", "USD", 2499, InvoiceStatus.Open, payInFull: false, daysAgo: 35);
        // Acme — draft
        AddInvoice(db, now, acme.Id, acmeSub.Id, "COM-INV-2006", "USD", 500, InvoiceStatus.Draft, payInFull: false, daysAgo: 0);
        // Soylent — voided
        AddInvoice(db, now, soylent.Id, null, "COM-INV-2007", "USD", 1000, InvoiceStatus.Open, payInFull: false, daysAgo: 60, voidIt: true);
    }

    private static void AddInvoice(
        CommerceDbContext db,
        DateTime now,
        Guid accountId,
        Guid? subscriptionId,
        string number,
        string currency,
        long amountMinor,
        InvoiceStatus initialStatus,
        bool payInFull,
        int daysAgo,
        long? partialPayMinor = null,
        bool voidIt = false)
    {
        var issued = now.AddDays(-daysAgo);
        var inv = Invoice.Create(accountId, subscriptionId, number, currency, issued, issued.AddDays(14), initialStatus, now);
        var line = InvoiceLine.Create(inv.Id, null, "Subscription period", 1, amountMinor, currency, null, null, now);
        inv.Recalculate(new[] { line }, now);
        db.Invoices.Add(inv);
        db.InvoiceLines.Add(line);
        if (payInFull)
        {
            inv.RegisterPayment(amountMinor, now);
        }
        else if (partialPayMinor is { } p)
        {
            inv.RegisterPayment(p, now);
        }
        if (voidIt)
        {
            inv.Void(now);
        }
    }

    // --------------------------------------------------------------- Payments

    private static void SeedPayments(
        CommerceDbContext db,
        DateTime now,
        BillingAccount acme, BillingAccount globex, BillingAccount initech)
    {
        var p1 = Payment.Create(acme.Id, null, null, PaymentProviderType.Stripe, "pi_acme_succ_1", null, 2499, "USD", PaymentStatus.Pending, now);
        p1.MarkSucceeded(now.AddMinutes(-3));
        var p2 = Payment.Create(acme.Id, null, null, PaymentProviderType.Stripe, "pi_acme_succ_2", null, 2499, "USD", PaymentStatus.Pending, now);
        p2.MarkSucceeded(now.AddDays(-30));
        var p3 = Payment.Create(globex.Id, null, null, PaymentProviderType.Stripe, "pi_globex_pending", null, 99900, "USD", PaymentStatus.Pending, now);
        var p4 = Payment.Create(initech.Id, null, null, PaymentProviderType.Stripe, "pi_initech_partial", null, 5000, "EUR", PaymentStatus.Pending, now);
        p4.MarkSucceeded(now.AddDays(-1));
        var p5 = Payment.Create(initech.Id, null, null, PaymentProviderType.Stripe, "pi_initech_failed", null, 9997, "EUR", PaymentStatus.Pending, now);
        p5.MarkFailed("card_declined", "Your card was declined.", now.AddHours(-6));
        var p6 = Payment.Create(acme.Id, null, null, PaymentProviderType.Stripe, "pi_acme_failed", null, 500, "USD", PaymentStatus.Pending, now);
        p6.MarkFailed("insufficient_funds", "Insufficient funds.", now.AddHours(-2));

        db.AddRange(p1, p2, p3, p4, p5, p6);
    }

    // -------------------------------------------------------- Provider events

    private static void SeedProviderEvents(CommerceDbContext db, DateTime now)
    {
        var e1 = PaymentProviderEventLog.Receive(PaymentProviderType.Stripe, "evt_inv_paid_001",
            "invoice.payment_succeeded",
            "{\"id\":\"evt_inv_paid_001\",\"data\":{\"object\":{\"amount_paid\":2499}}}", now.AddMinutes(-2));
        e1.MarkProcessed(now.AddMinutes(-1));

        var e2 = PaymentProviderEventLog.Receive(PaymentProviderType.Stripe, "evt_pi_succ_002",
            "payment_intent.succeeded",
            "{\"id\":\"evt_pi_succ_002\"}", now.AddDays(-1));
        e2.MarkProcessed(now.AddDays(-1).AddMinutes(1));

        var e3 = PaymentProviderEventLog.Receive(PaymentProviderType.Stripe, "evt_pi_fail_003",
            "payment_intent.payment_failed",
            "{\"id\":\"evt_pi_fail_003\",\"data\":{\"object\":{\"last_payment_error\":{\"code\":\"card_declined\"}}}}", now.AddHours(-7));
        e3.MarkFailed("Downstream service threw NullReferenceException while linking payment to invoice.", now.AddHours(-7).AddMinutes(2));

        var e4 = PaymentProviderEventLog.Receive(PaymentProviderType.Stripe, "evt_ping_004",
            "customer.created",
            "{\"id\":\"evt_ping_004\"}", now.AddDays(-3));
        e4.MarkIgnored("customer.created is not handled by Commerce.", now.AddDays(-3).AddMinutes(1));

        var e5 = PaymentProviderEventLog.Receive(PaymentProviderType.Stripe, "evt_inv_open_005",
            "invoice.created",
            "{\"id\":\"evt_inv_open_005\"}", now.AddMinutes(-30));

        var e6 = PaymentProviderEventLog.Receive(PaymentProviderType.Stripe, "evt_chg_dispute_006",
            "charge.dispute.created",
            "{\"id\":\"evt_chg_dispute_006\"}", now.AddDays(-2));
        e6.MarkFailed("Dispute handler not implemented.", now.AddDays(-2).AddMinutes(5));

        db.AddRange(e1, e2, e3, e4, e5, e6);
    }
}
