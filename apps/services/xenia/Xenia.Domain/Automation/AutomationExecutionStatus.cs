namespace Xenia.Domain.Automation;

public enum AutomationExecutionStatus
{
    Queued              = 0,
    Running             = 1,
    Completed           = 2,
    CompletedWithErrors = 3,
    Failed              = 4,
    Cancelled           = 5,
    DeadLettered        = 6,
    TimedOut            = 7,
}
