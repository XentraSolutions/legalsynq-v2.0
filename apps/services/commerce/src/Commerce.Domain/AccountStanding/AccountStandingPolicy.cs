namespace Commerce.Domain.AccountStanding;

/// <summary>
/// Configurable policy values for account-standing evaluation. Kept as
/// a domain value object so the engine never depends on the
/// configuration plumbing directly.
/// </summary>
public sealed record AccountStandingPolicy(int GracePeriodDays, int PastDueToSuspendedDays)
{
    public static AccountStandingPolicy Default { get; } = new(GracePeriodDays: 7, PastDueToSuspendedDays: 14);
}
