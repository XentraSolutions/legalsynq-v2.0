using Commerce.Application.Common.Time;
using Commerce.Application.Integration.Abstractions;
using Commerce.Domain.AccountStanding;
using Commerce.Domain.AccountStanding.Enums;
using Commerce.Domain.Billing;
using Commerce.Domain.Catalog;
using Commerce.Domain.Catalog.Enums;
using Commerce.Domain.Subscriptions;
using Commerce.Infrastructure.Integration.HostAdapters;
using Commerce.Infrastructure.Integration.Services;
using Commerce.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using AccountStandingEntity = Commerce.Domain.AccountStanding.AccountStanding;

namespace Commerce.Tests.Integration;

internal sealed class IntegrationFixedClock : IClock
{
    public DateTime UtcNow { get; set; } = new(2026, 7, 1, 12, 0, 0, DateTimeKind.Utc);
}

/// <summary>
/// In-memory test host that wires the COM-B08 integration services
/// (entitlement snapshot + access recommendation) over a Commerce
/// DbContext, then exposes seeding helpers for the contract tests.
/// </summary>
internal sealed class IntegrationTestHost : IDisposable
{
    public CommerceDbContext Db { get; }
    public IntegrationFixedClock Clock { get; } = new();
    public NoopHostTenantResolver TenantResolver { get; }
    public CommerceAccessRecommendationService Recommendation { get; }
    public CommerceEntitlementSnapshotService Snapshot { get; }
    public LocalHostIdentityContextAccessor Identity { get; } = new();
    public NoopProvisioningHookPublisher Provisioning { get; } =
        new(NullLogger<NoopProvisioningHookPublisher>.Instance);

    public IntegrationTestHost()
    {
        var opts = new DbContextOptionsBuilder<CommerceDbContext>()
            .UseInMemoryDatabase($"integration-tests-{Guid.CreateVersion7()}")
            .Options;
        Db = new CommerceDbContext(opts);
        TenantResolver = new NoopHostTenantResolver(Db);
        Recommendation = new CommerceAccessRecommendationService(Db, Clock, TenantResolver);
        Snapshot = new CommerceEntitlementSnapshotService(Db, Clock, TenantResolver, Recommendation);
    }

    /// <summary>
    /// Seed a billing account with optional external-ref + standing +
    /// active subscription on a single plan with one feature limit.
    /// Returns the billing-account id and the seeded plan/product
    /// keys.
    /// </summary>
    public SeedResult SeedAccount(
        string accountNumber = "COM-ACC-INT1",
        string displayName = "Acme Integration",
        string hostKey = "legalsynq",
        string? externalTenantId = "tenant-abc",
        AccountStandingStatus? standing = AccountStandingStatus.Good,
        SeededSubscriptionShape subscriptionShape = SeededSubscriptionShape.Active)
    {
        var now = Clock.UtcNow;

        var product = Product.Create("k-prod", "Product", null, 0, now);
        product.Activate(now);
        var plan = Plan.Create(product.Id, "k-plan", "Plan", null, BillingInterval.Monthly, null, 0, now);
        plan.Activate(now);
        var feature = Feature.Create(product.Id, "k-feat", "Feature", null, FeatureType.Boolean, now);
        feature.Activate(now);
        var planFeature = PlanFeature.Create(plan.Id, feature.Id, isEnabled: true, limitValue: 100, meteredIncludedUnits: null, now);
        var price = Price.Create(plan.Id, null, null, "USD", 1999, BillingInterval.Monthly, now.AddMinutes(-5), null, now);
        Db.AddRange(product, plan, feature, planFeature, price);

        var account = BillingAccount.Create(accountNumber, displayName, null, "USD", now);
        account.Activate(now);
        Db.BillingAccounts.Add(account);

        if (externalTenantId is not null)
        {
            Db.BillingAccountExternalRefs.Add(BillingAccountExternalRef.Create(
                account.Id, hostKey, externalTenantId, externalCustomerRef: null, isPrimary: true, now));
        }

        if (standing is not null)
        {
            var s = AccountStandingEntity.Create(account.Id, now);
            s.Apply(standing.Value, reason: "seeded", gracePeriodEndsAtUtc: standing == AccountStandingStatus.GracePeriod ? now.AddDays(7) : null,
                pastDueSinceUtc: standing == AccountStandingStatus.PastDue ? now.AddDays(-1) : null,
                suspendedAtUtc: standing == AccountStandingStatus.Suspended ? now.AddDays(-1) : null,
                nowUtc: now);
            Db.AccountStandings.Add(s);
        }

        if (subscriptionShape != SeededSubscriptionShape.None)
        {
            var trialStart = subscriptionShape == SeededSubscriptionShape.Trialing ? (DateTime?)now : null;
            var trialEnd = subscriptionShape == SeededSubscriptionShape.Trialing ? (DateTime?)now.AddDays(14) : null;
            var sub = Subscription.Create(
                account.Id, $"COM-SUB-{accountNumber}",
                now, now, now.AddMonths(1), trialStart, trialEnd, now);
            if (subscriptionShape == SeededSubscriptionShape.Cancelled)
            {
                sub.Cancel(cancelAtPeriodEnd: false, reason: "test", now);
            }
            Db.Subscriptions.Add(sub);

            if (subscriptionShape != SeededSubscriptionShape.Cancelled)
            {
                var item = SubscriptionItem.Create(
                    sub.Id, plan.Id, price.Id, quantity: 1, unitAmountMinor: 1999,
                    currency: "USD", interval: BillingInterval.Monthly,
                    effectiveFromUtc: now, nowUtc: now);
                Db.SubscriptionItems.Add(item);
            }
        }

        Db.SaveChanges();
        return new SeedResult(account.Id, hostKey, externalTenantId, plan.Id, plan.Key, product.Id, product.Key);
    }

    public void Dispose() => Db.Dispose();
}

internal enum SeededSubscriptionShape
{
    None,
    Active,
    Trialing,
    Cancelled,
}

internal sealed record SeedResult(
    Guid BillingAccountId,
    string HostPlatformKey,
    string? ExternalTenantId,
    Guid PlanId,
    string PlanKey,
    Guid ProductId,
    string ProductKey);
