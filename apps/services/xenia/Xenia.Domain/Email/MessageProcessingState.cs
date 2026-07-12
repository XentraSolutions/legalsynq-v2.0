namespace Xenia.Domain.Email;

/// <summary>Post-import processing lifecycle state of an ingested message.</summary>
public enum MessageProcessingState
{
    /// <summary>Imported; awaiting any downstream processing.</summary>
    Pending = 0,

    /// <summary>Currently being processed by a downstream handler.</summary>
    Processing = 1,

    /// <summary>All downstream processing complete.</summary>
    Processed = 2,

    /// <summary>Downstream processing produced an error; may be retried.</summary>
    Error = 3,

    /// <summary>Message was intentionally skipped by policy.</summary>
    Skipped = 4,
}
