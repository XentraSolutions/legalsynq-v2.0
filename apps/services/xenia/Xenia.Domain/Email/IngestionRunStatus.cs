namespace Xenia.Domain.Email;

/// <summary>Lifecycle status of an email ingestion run.</summary>
public enum IngestionRunStatus
{
    Queued               = 0,
    Running              = 1,
    Completed            = 2,
    CompletedWithErrors  = 3,
    Failed               = 4,
    Cancelled            = 5,
    Interrupted          = 6,
}
