namespace CareConnect.Domain;

public static class NotificationType
{
    public const string ReferralStatusChanged    = "ReferralStatusChanged";
    // LSCC-005: new referral notification types
    public const string ReferralCreated          = "ReferralCreated";
    public const string ReferralAcceptedProvider = "ReferralAcceptedProvider";
    public const string ReferralAcceptedReferrer = "ReferralAcceptedReferrer";
    public const string AppointmentScheduled     = "AppointmentScheduled";
    public const string AppointmentConfirmed     = "AppointmentConfirmed";
    public const string AppointmentCancelled     = "AppointmentCancelled";
    public const string AppointmentReminder      = "AppointmentReminder";
    // LSCC-005-01: hardening notification types
    public const string ReferralEmailResent      = "ReferralEmailResent";
    // LSCC-005-02: automatic retry audit marker
    public const string ReferralEmailAutoRetry   = "ReferralEmailAutoRetry";

    public static readonly IReadOnlySet<string> All = new HashSet<string>
    {
        ReferralStatusChanged, ReferralCreated,
        ReferralAcceptedProvider, ReferralAcceptedReferrer,
        AppointmentScheduled, AppointmentConfirmed,
        AppointmentCancelled, AppointmentReminder,
        ReferralEmailResent, ReferralEmailAutoRetry,
    };

    public static bool IsValid(string value) => All.Contains(value);
}
