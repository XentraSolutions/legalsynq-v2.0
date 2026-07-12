namespace Xenia.Domain.Automation;

public enum AutomationLifecycleState
{
    Registered  = 0,
    Disabled    = 1,
    Enabled     = 2,
    Degraded    = 3,
    Unavailable = 4,
    Retired     = 5,
}
