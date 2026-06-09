namespace Commerce.Domain.Subscriptions.Enums;

public enum SubscriptionChangeType
{
    Created = 0,
    TrialStarted = 1,
    TrialEnded = 2,
    Activated = 3,
    PlanChanged = 4,
    Cancelled = 5,
    Renewed = 6,
    Suspended = 7,
    Reactivated = 8,
    Expired = 9
}
