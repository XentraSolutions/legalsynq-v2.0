namespace Xenia.Domain.Automation;

public enum AutomationConcurrencyPolicy
{
    AllowConcurrent,
    SkipIfRunning,
    WaitForCompletion,
}
