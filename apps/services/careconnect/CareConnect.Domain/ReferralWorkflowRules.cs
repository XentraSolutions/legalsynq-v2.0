using BuildingBlocks.Exceptions;

namespace CareConnect.Domain;

public static class ReferralWorkflowRules
{
    private static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> AllowedTransitions =
        new Dictionary<string, IReadOnlyList<string>>
        {
            [Referral.ValidStatuses.New]       = new[] { Referral.ValidStatuses.Received,  Referral.ValidStatuses.Cancelled },
            [Referral.ValidStatuses.Received]  = new[] { Referral.ValidStatuses.Contacted, Referral.ValidStatuses.Cancelled },
            [Referral.ValidStatuses.Contacted] = new[] { Referral.ValidStatuses.Scheduled, Referral.ValidStatuses.Cancelled },
            [Referral.ValidStatuses.Scheduled] = new[] { Referral.ValidStatuses.Completed, Referral.ValidStatuses.Cancelled },
            [Referral.ValidStatuses.Completed] = Array.Empty<string>(),
            [Referral.ValidStatuses.Cancelled] = Array.Empty<string>(),
        };

    public static bool IsValidTransition(string fromStatus, string toStatus)
    {
        if (!AllowedTransitions.TryGetValue(fromStatus, out var allowed))
            return false;

        return allowed.Contains(toStatus);
    }

    public static void ValidateTransition(string fromStatus, string toStatus)
    {
        if (fromStatus == toStatus)
            return;

        if (!IsValidTransition(fromStatus, toStatus))
            throw new ValidationException(
                "One or more validation errors occurred.",
                new Dictionary<string, string[]>
                {
                    ["status"] = new[] { $"Invalid referral status transition from {fromStatus} to {toStatus}." }
                });
    }
}
