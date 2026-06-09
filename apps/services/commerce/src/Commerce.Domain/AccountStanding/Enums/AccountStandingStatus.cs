namespace Commerce.Domain.AccountStanding.Enums;

public enum AccountStandingStatus
{
    Good = 0,
    Trialing = 1,
    GracePeriod = 2,
    PastDue = 3,
    Suspended = 4,
    Cancelled = 5,
    Closed = 6
}
