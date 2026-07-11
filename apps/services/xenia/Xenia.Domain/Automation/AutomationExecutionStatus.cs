namespace Xenia.Domain.Automation;

public enum AutomationExecutionStatus
{
    Queued      = 0,
    Running     = 1,
    Completed   = 2,
    Failed      = 3,
    Cancelled   = 4,
    DeadLettered = 5,
    TimedOut    = 6,
}
