namespace Xenia.Domain.Email;

/// <summary>The dispatch lifecycle of an attachment reference.</summary>
public enum AttachmentDispatchStatus
{
    /// <summary>Detected but not yet dispatched to the Documents adapter.</summary>
    Pending    = 0,

    /// <summary>Successfully sent to the Documents adapter; document reference obtained.</summary>
    Dispatched = 1,

    /// <summary>Dispatch failed; may be retried.</summary>
    Failed     = 2,

    /// <summary>Intentionally skipped (e.g. exceeds size limit).</summary>
    Skipped    = 3,
}
