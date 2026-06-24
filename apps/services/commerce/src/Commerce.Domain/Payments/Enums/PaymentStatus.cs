namespace Commerce.Domain.Payments.Enums;

public enum PaymentStatus
{
    Pending = 0,
    Succeeded = 1,
    Failed = 2,
    Cancelled = 3,
    Refunded = 4
}

public enum PaymentAttemptStatus
{
    Started = 0,
    Succeeded = 1,
    Failed = 2,
    Ignored = 3
}
