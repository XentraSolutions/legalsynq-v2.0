namespace Xenia.Domain.Email;

/// <summary>
/// Classifies the condition that caused an operational alert to open.
/// </summary>
public enum EmailAlertType
{
    SourceAuthenticationFailure    = 1,
    SourceConnectionFailure        = 2,
    SourceRepeatedFailure          = 3,
    SourceDisabled                 = 4,
    ProviderUnavailable            = 5,
    ProviderRateLimited            = 6,
    SyncStalled                    = 7,
    LockStale                      = 8,
    CursorInvalidated              = 9,
    AttachmentDispatchFailure      = 10,
    AuditUnavailable               = 11,
    DocumentsUnavailable           = 12,
    SecretProviderUnavailable      = 13,
    RetentionFailure               = 14,
    MigrationMismatch              = 15,
    WorkerUnavailable              = 16,
}
