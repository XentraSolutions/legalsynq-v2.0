namespace Xenia.Domain.Automation;

public enum AutomationTriggerType
{
    Manual      = 0,
    Interval    = 1,
    CronLike    = 2,
    EventDriven = 3,
    Retry       = 4,
    OneTime     = 5,
}
