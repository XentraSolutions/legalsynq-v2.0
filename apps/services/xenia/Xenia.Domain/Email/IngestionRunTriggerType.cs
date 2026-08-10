namespace Xenia.Domain.Email;

/// <summary>What triggered this ingestion run.</summary>
public enum IngestionRunTriggerType
{
    Manual    = 1,
    Scheduled = 2,
    Retry     = 3,
    Resume    = 4,
    Initial   = 5,
}
