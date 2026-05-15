using Commerce.Domain.AccountStanding.Enums;

namespace Commerce.Contracts.AccountStanding;

public sealed record AccountStandingResponse(
    Guid Id,
    Guid BillingAccountId,
    AccountStandingStatus Status,
    string? Reason,
    DateTime? GracePeriodEndsAtUtc,
    DateTime? PastDueSinceUtc,
    DateTime? SuspendedAtUtc,
    DateTime LastEvaluatedAtUtc,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);
