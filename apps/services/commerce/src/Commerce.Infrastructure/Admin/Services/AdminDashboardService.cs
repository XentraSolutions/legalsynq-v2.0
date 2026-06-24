using Commerce.Application.Admin.Abstractions;
using Commerce.Application.Common.Time;
using Commerce.Contracts.Admin;
using Commerce.Domain.AccountStanding.Enums;
using Commerce.Domain.Catalog.Enums;
using Commerce.Domain.Billing.Enums;
using Commerce.Domain.Invoicing.Enums;
using Commerce.Domain.Payments.Enums;
using Commerce.Domain.Subscriptions.Enums;
using Commerce.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Commerce.Infrastructure.Admin.Services;

/// <summary>
/// Pure read-model implementation of <see cref="IAdminDashboardService"/>.
/// All queries are projection-only and never write to the database. Designed
/// to be safe for repeated polling from the admin UI.
/// </summary>
internal sealed class AdminDashboardService : IAdminDashboardService
{
    private readonly CommerceDbContext _db;
    private readonly IClock _clock;

    public AdminDashboardService(CommerceDbContext db, IClock clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<AdminDashboardSummaryResponse> GetSummaryAsync(CancellationToken ct)
    {
        var productTotal = await _db.Products.CountAsync(ct);
        var productActive = await _db.Products.CountAsync(p => p.Status == CatalogStatus.Active, ct);
        var plansCount = await _db.Plans.CountAsync(ct);
        var bundlesCount = await _db.Bundles.CountAsync(ct);
        var addonsCount = await _db.Addons.CountAsync(ct);
        var pricesCount = await _db.Prices.CountAsync(ct);

        var billingByStatus = await GroupCountsAsync(_db.BillingAccounts, b => b.Status, ct);
        var subsByStatus = await GroupCountsAsync(_db.Subscriptions, s => s.Status, ct);
        var invByStatus = await GroupCountsAsync(_db.Invoices, i => i.Status, ct);
        var payByStatus = await GroupCountsAsync(_db.Payments, p => p.Status, ct);
        var eventsByStatus = await GroupCountsAsync(_db.PaymentProviderEventLogs, e => e.ProcessingStatus, ct);

        return new AdminDashboardSummaryResponse(
            Catalog: new CatalogCountsResponse(
                Products: productTotal,
                ActiveProducts: productActive,
                Plans: plansCount,
                Bundles: bundlesCount,
                Addons: addonsCount,
                Prices: pricesCount),
            BillingAccounts: new BillingAccountCountsResponse(
                Total: billingByStatus.Values.Sum(),
                Active: billingByStatus.GetValueOrDefault(BillingAccountStatus.Active),
                Suspended: billingByStatus.GetValueOrDefault(BillingAccountStatus.Suspended),
                Closed: billingByStatus.GetValueOrDefault(BillingAccountStatus.Closed)),
            Subscriptions: new SubscriptionCountsResponse(
                Total: subsByStatus.Values.Sum(),
                Trialing: subsByStatus.GetValueOrDefault(SubscriptionStatus.Trialing),
                Active: subsByStatus.GetValueOrDefault(SubscriptionStatus.Active),
                PastDue: subsByStatus.GetValueOrDefault(SubscriptionStatus.PastDue),
                Suspended: subsByStatus.GetValueOrDefault(SubscriptionStatus.Suspended),
                Cancelled: subsByStatus.GetValueOrDefault(SubscriptionStatus.Cancelled),
                Expired: subsByStatus.GetValueOrDefault(SubscriptionStatus.Expired)),
            Invoices: new InvoiceCountsResponse(
                Total: invByStatus.Values.Sum(),
                Draft: invByStatus.GetValueOrDefault(InvoiceStatus.Draft),
                Open: invByStatus.GetValueOrDefault(InvoiceStatus.Open),
                Paid: invByStatus.GetValueOrDefault(InvoiceStatus.Paid),
                Void: invByStatus.GetValueOrDefault(InvoiceStatus.Void),
                Uncollectible: invByStatus.GetValueOrDefault(InvoiceStatus.Uncollectible)),
            Payments: new PaymentCountsResponse(
                Total: payByStatus.Values.Sum(),
                Pending: payByStatus.GetValueOrDefault(PaymentStatus.Pending),
                Succeeded: payByStatus.GetValueOrDefault(PaymentStatus.Succeeded),
                Failed: payByStatus.GetValueOrDefault(PaymentStatus.Failed),
                Cancelled: payByStatus.GetValueOrDefault(PaymentStatus.Cancelled)),
            ProviderEvents: new ProviderEventCountsResponse(
                Total: eventsByStatus.Values.Sum(),
                Received: eventsByStatus.GetValueOrDefault(PaymentProviderEventProcessingStatus.Received),
                Processed: eventsByStatus.GetValueOrDefault(PaymentProviderEventProcessingStatus.Processed),
                Failed: eventsByStatus.GetValueOrDefault(PaymentProviderEventProcessingStatus.Failed),
                Ignored: eventsByStatus.GetValueOrDefault(PaymentProviderEventProcessingStatus.Ignored)),
            GeneratedAtUtc: _clock.UtcNow);
    }

    public async Task<RevenueSummaryResponse> GetRevenueSummaryAsync(CancellationToken ct)
    {
        // Sum amounts by currency over Invoices: Paid → revenue collected,
        // Open → outstanding balance. Draft/Void/Uncollectible are excluded
        // from both totals because they do not represent realised revenue
        // nor expected collection.
        var raw = await _db.Invoices
            .Where(i => i.Status == InvoiceStatus.Paid || i.Status == InvoiceStatus.Open)
            .GroupBy(i => new { i.Currency, i.Status })
            .Select(g => new
            {
                g.Key.Currency,
                g.Key.Status,
                Count = g.Count(),
                Total = g.Sum(x => (long?)x.TotalAmountMinor) ?? 0L,
                Due = g.Sum(x => (long?)x.AmountDueMinor) ?? 0L
            })
            .ToListAsync(ct);

        var byCurrency = raw
            .GroupBy(r => r.Currency)
            .Select(g => new CurrencyRevenueResponse(
                Currency: g.Key,
                PaidAmountMinor: g.Where(r => r.Status == InvoiceStatus.Paid).Sum(r => r.Total),
                OutstandingAmountMinor: g.Where(r => r.Status == InvoiceStatus.Open).Sum(r => r.Due),
                PaidInvoiceCount: g.Where(r => r.Status == InvoiceStatus.Paid).Sum(r => r.Count),
                OpenInvoiceCount: g.Where(r => r.Status == InvoiceStatus.Open).Sum(r => r.Count)))
            .OrderBy(c => c.Currency, StringComparer.Ordinal)
            .ToList();

        return new RevenueSummaryResponse(byCurrency, _clock.UtcNow);
    }

    public async Task<AccountStandingSummaryResponse> GetAccountStandingSummaryAsync(CancellationToken ct)
    {
        var rows = await GroupCountsAsync(_db.AccountStandings, a => a.Status, ct);

        var dict = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (AccountStandingStatus s in Enum.GetValues(typeof(AccountStandingStatus)))
        {
            dict[s.ToString()] = rows.GetValueOrDefault(s);
        }

        return new AccountStandingSummaryResponse(
            CountsByStatus: dict,
            TotalEvaluated: rows.Values.Sum(),
            GeneratedAtUtc: _clock.UtcNow);
    }

    public async Task<ProviderEventSummaryResponse> GetProviderEventSummaryAsync(CancellationToken ct)
    {
        var rows = await _db.PaymentProviderEventLogs
            .GroupBy(e => new { e.Provider, e.ProcessingStatus })
            .Select(g => new
            {
                g.Key.Provider,
                g.Key.ProcessingStatus,
                EventCount = g.Count(),
                LastUtc = g.Max(x => (DateTime?)x.CreatedAtUtc)
            })
            .ToListAsync(ct);

        var groups = rows
            .Select(r => new ProviderEventGroupResponse(
                Provider: r.Provider.ToString(),
                Status: r.ProcessingStatus.ToString(),
                Count: r.EventCount,
                LastEventUtc: r.LastUtc))
            .OrderBy(g => g.Provider, StringComparer.Ordinal)
            .ThenBy(g => g.Status, StringComparer.Ordinal)
            .ToList();

        return new ProviderEventSummaryResponse(
            Groups: groups,
            TotalEvents: groups.Sum(g => g.Count),
            GeneratedAtUtc: _clock.UtcNow);
    }

    public async Task<RecentActivityResponse> GetRecentActivityAsync(int take, CancellationToken ct)
    {
        if (take <= 0) take = 10;
        if (take > 50) take = 50;

        // Pull a small slice from each of four streams, then merge + sort
        // by occurrence in memory. Each stream is bounded by `take` so this
        // remains O(take) work per stream regardless of total volume.
        var invoices = await _db.Invoices
            .OrderByDescending(i => i.CreatedAtUtc)
            .Take(take)
            .Select(i => new RecentActivityEntryResponse(
                "Invoice",
                i.Id,
                i.InvoiceNumber,
                i.Status.ToString(),
                i.CreatedAtUtc))
            .ToListAsync(ct);

        var payments = await _db.Payments
            .OrderByDescending(p => p.CreatedAtUtc)
            .Take(take)
            .Select(p => new RecentActivityEntryResponse(
                "Payment",
                p.Id,
                p.Currency + " " + p.AmountMinor,
                p.Status.ToString(),
                p.CreatedAtUtc))
            .ToListAsync(ct);

        var subs = await _db.Subscriptions
            .OrderByDescending(s => s.CreatedAtUtc)
            .Take(take)
            .Select(s => new RecentActivityEntryResponse(
                "Subscription",
                s.Id,
                s.SubscriptionNumber,
                s.Status.ToString(),
                s.CreatedAtUtc))
            .ToListAsync(ct);

        var events = await _db.PaymentProviderEventLogs
            .OrderByDescending(e => e.CreatedAtUtc)
            .Take(take)
            .Select(e => new RecentActivityEntryResponse(
                "ProviderEvent",
                e.Id,
                e.Provider.ToString() + " · " + e.EventType,
                e.ProcessingStatus.ToString(),
                e.CreatedAtUtc))
            .ToListAsync(ct);

        var merged = invoices
            .Concat(payments)
            .Concat(subs)
            .Concat(events)
            .OrderByDescending(e => e.OccurredAtUtc)
            .Take(take)
            .ToList();

        return new RecentActivityResponse(merged, _clock.UtcNow);
    }

    private static async Task<Dictionary<TKey, int>> GroupCountsAsync<TEntity, TKey>(
        IQueryable<TEntity> source,
        System.Linq.Expressions.Expression<Func<TEntity, TKey>> selector,
        CancellationToken ct) where TKey : notnull
    {
        var rows = await source
            .GroupBy(selector)
            .Select(g => new { Key = g.Key, Count = g.Count() })
            .ToListAsync(ct);
        return rows.ToDictionary(r => r.Key, r => r.Count);
    }
}
