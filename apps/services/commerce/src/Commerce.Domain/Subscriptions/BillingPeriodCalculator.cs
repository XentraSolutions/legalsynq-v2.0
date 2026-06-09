using Commerce.Domain.Catalog.Enums;

namespace Commerce.Domain.Subscriptions;

/// <summary>
/// Pure helper for computing the next subscription period end given a
/// billing interval and an anchor start. Lives in domain so service
/// code (and tests) reuse the exact same arithmetic.
/// </summary>
public static class BillingPeriodCalculator
{
    /// <summary>
    /// Default OneTime period length used when no explicit end is
    /// supplied: arbitrarily long (100 years). Documented as a sentinel
    /// so future invoicing code knows it cannot rely on it for billing.
    /// </summary>
    public static readonly TimeSpan OneTimeDefaultLength = TimeSpan.FromDays(365 * 100);

    public static DateTime NextPeriodEnd(DateTime periodStartUtc, BillingInterval interval)
    {
        return interval switch
        {
            BillingInterval.Monthly => periodStartUtc.AddMonths(1),
            BillingInterval.Annual => periodStartUtc.AddYears(1),
            BillingInterval.OneTime => periodStartUtc.Add(OneTimeDefaultLength),
            BillingInterval.Custom => throw new InvalidOperationException(
                "Custom billing interval requires an explicit period end and is not supported in COM-B04."),
            _ => throw new InvalidOperationException($"Unknown BillingInterval '{interval}'.")
        };
    }
}
