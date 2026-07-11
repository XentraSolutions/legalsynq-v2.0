namespace Xenia.Domain.Automation;

public enum AutomationDeadLetterStatus
{
    Open      = 0,
    Retrying  = 1,
    Resolved  = 2,
    Abandoned = 3,
}
