namespace Xenia.Domain.Email;

/// <summary>Execution mode for a retention run.</summary>
public enum EmailRetentionMode
{
    DryRun  = 1,
    Execute = 2,
}

/// <summary>Lifecycle status of a retention run.</summary>
public enum EmailRetentionRunStatus
{
    Running   = 1,
    Completed = 2,
    Failed    = 3,
    Cancelled = 4,
}
