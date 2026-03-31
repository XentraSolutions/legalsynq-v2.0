using BuildingBlocks.Exceptions;

namespace CareConnect.Domain;

public static class ReferralWorkflowRules
{
    private static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> AllowedTransitions =
        new Dictionary<string, IReadOnlyList<string>>
        {
            [Referral.ValidStatuses.New]       = new[] { Referral.ValidStatuses.Accepted, Referral.ValidStatuses.Declined, Referral.ValidStatuses.Cancelled },
            [Referral.ValidStatuses.Accepted]  = new[] { Referral.ValidStatuses.Scheduled, Referral.ValidStatuses.Declined, Referral.ValidStatuses.Cancelled },
            [Referral.ValidStatuses.Scheduled] = new[] { Referral.ValidStatuses.Completed, Referral.ValidStatuses.Cancelled },
            [Referral.ValidStatuses.Completed] = Array.Empty<string>(),
            [Referral.ValidStatuses.Declined]  = Array.Empty<string>(),
            [Referral.ValidStatuses.Cancelled] = Array.Empty<string>(),

            // Legacy status values kept for data that pre-dates the canonical migration.
            // They are treated as read-only states; transitions out follow the Accepted path.
            [Referral.ValidStatuses.Legacy.Received]  = new[] { Referral.ValidStatuses.Accepted, Referral.ValidStatuses.Scheduled, Referral.ValidStatuses.Declined, Referral.ValidStatuses.Cancelled },
            [Referral.ValidStatuses.Legacy.Contacted] = new[] { Referral.ValidStatuses.Accepted, Referral.ValidStatuses.Scheduled, Referral.ValidStatuses.Declined, Referral.ValidStatuses.Cancelled },
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

    /// <summary>
    /// Returns true when the status is a terminal state from which no transitions are permitted.
    /// </summary>
    public static bool IsTerminal(string status) =>
        status is Referral.ValidStatuses.Completed
               or Referral.ValidStatuses.Declined
               or Referral.ValidStatuses.Cancelled;

    /// <summary>
    /// Determines the capability code required to perform the given status transition.
    /// Used by endpoints to enforce capability-based authorization on referral updates.
    /// </summary>
    public static string RequiredCapabilityFor(string toStatus) => toStatus switch
    {
        Referral.ValidStatuses.Accepted  => BuildingBlocks.Authorization.CapabilityCodes.ReferralAccept,
        Referral.ValidStatuses.Declined  => BuildingBlocks.Authorization.CapabilityCodes.ReferralDecline,
        Referral.ValidStatuses.Cancelled => BuildingBlocks.Authorization.CapabilityCodes.ReferralCancel,
        _                                => BuildingBlocks.Authorization.CapabilityCodes.ReferralUpdateStatus,
    };
}
