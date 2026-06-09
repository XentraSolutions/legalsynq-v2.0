namespace Commerce.Contracts.Admin;

/// <summary>
/// Top-level admin dashboard summary aggregating headline counts across all
/// COM-B0x modules. Read-only; safe for repeated polling.
/// </summary>
public sealed record AdminDashboardSummaryResponse(
    CatalogCountsResponse Catalog,
    BillingAccountCountsResponse BillingAccounts,
    SubscriptionCountsResponse Subscriptions,
    InvoiceCountsResponse Invoices,
    PaymentCountsResponse Payments,
    ProviderEventCountsResponse ProviderEvents,
    DateTime GeneratedAtUtc);

public sealed record CatalogCountsResponse(
    int Products,
    int ActiveProducts,
    int Plans,
    int Bundles,
    int Addons,
    int Prices);

public sealed record BillingAccountCountsResponse(
    int Total,
    int Active,
    int Suspended,
    int Closed);

public sealed record SubscriptionCountsResponse(
    int Total,
    int Trialing,
    int Active,
    int PastDue,
    int Suspended,
    int Cancelled,
    int Expired);

public sealed record InvoiceCountsResponse(
    int Total,
    int Draft,
    int Open,
    int Paid,
    int Void,
    int Uncollectible);

public sealed record PaymentCountsResponse(
    int Total,
    int Pending,
    int Succeeded,
    int Failed,
    int Cancelled);

public sealed record ProviderEventCountsResponse(
    int Total,
    int Received,
    int Processed,
    int Failed,
    int Ignored);

/// <summary>
/// Revenue rollups in minor currency units, grouped by currency. Includes
/// totals collected (paid invoices) and outstanding (open invoice balances).
/// </summary>
public sealed record RevenueSummaryResponse(
    IReadOnlyList<CurrencyRevenueResponse> ByCurrency,
    DateTime GeneratedAtUtc);

public sealed record CurrencyRevenueResponse(
    string Currency,
    long PaidAmountMinor,
    long OutstandingAmountMinor,
    int PaidInvoiceCount,
    int OpenInvoiceCount);

/// <summary>
/// Account-standing distribution. Status keys mirror
/// <c>Commerce.Domain.AccountStanding.Enums.AccountStandingStatus</c>.
/// </summary>
public sealed record AccountStandingSummaryResponse(
    IReadOnlyDictionary<string, int> CountsByStatus,
    int TotalEvaluated,
    DateTime GeneratedAtUtc);

/// <summary>
/// Provider-event rollup grouped by provider and processing status, useful
/// for the operability dashboard's "Provider Events" tile.
/// </summary>
public sealed record ProviderEventSummaryResponse(
    IReadOnlyList<ProviderEventGroupResponse> Groups,
    int TotalEvents,
    DateTime GeneratedAtUtc);

public sealed record ProviderEventGroupResponse(
    string Provider,
    string Status,
    int Count,
    DateTime? LastEventUtc);

/// <summary>
/// A small, mixed-stream activity feed combining the most recent items from
/// several modules. Each entry is intentionally generic so the admin UI can
/// render a unified list without schema-coupling to every module DTO.
/// </summary>
public sealed record RecentActivityResponse(
    IReadOnlyList<RecentActivityEntryResponse> Entries,
    DateTime GeneratedAtUtc);

public sealed record RecentActivityEntryResponse(
    string Kind,
    Guid Id,
    string Summary,
    string Status,
    DateTime OccurredAtUtc);
