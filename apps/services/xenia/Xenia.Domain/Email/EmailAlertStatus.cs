namespace Xenia.Domain.Email;

/// <summary>Lifecycle status of an operational alert.</summary>
public enum EmailAlertStatus
{
    Open         = 1,
    Acknowledged = 2,
    Resolved     = 3,
    Suppressed   = 4,
}
