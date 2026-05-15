namespace Commerce.Domain.Subscriptions.Enums;

public enum SubscriptionStatus
{
    Draft = 0,
    Trialing = 1,
    Active = 2,
    PastDue = 3,
    Suspended = 4,
    Cancelled = 5,
    Expired = 6
}
