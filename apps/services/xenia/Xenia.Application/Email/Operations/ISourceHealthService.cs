using Xenia.Domain.Email;

namespace Xenia.Application.Email.Operations;

/// <summary>
/// Returns per-source operational health snapshots for a tenant.
///
/// Security: never exposes raw cursors, tokens, secrets, or credentials.
/// </summary>
public interface ISourceHealthService
{
    Task<IReadOnlyList<SourceHealthSnapshot>> GetAllAsync(Guid tenantId, CancellationToken ct = default);
    Task<SourceHealthSnapshot?> GetAsync(Guid tenantId, Guid sourceId, CancellationToken ct = default);
}

public sealed record SourceHealthSnapshot(
    Guid SourceId,
    string SourceName,
    EmailProviderType Provider,
    bool IsEnabled,
    bool ModuleEffectivelyEnabled,
    EmailSourceStatus ConnectionState,
    EmailValidationStatus? ValidationState,
    EmailHealthStatus HealthState,
    DateTime? LastValidationAt,
    DateTime? LastSuccessfulValidationAt,
    DateTime? LastSyncAttemptAt,
    DateTime? LastSuccessfulSyncAt,
    Guid? CurrentRunId,
    int ConsecutiveFailureCount,
    DateTime? NextRetryAt,
    bool IsLocked,
    string? LockOwnerSafeId,
    DateTime? LockExpiresAt,
    SyncCursorType? CursorType,
    string? SafeCursorSummary,
    bool InitialSyncComplete,
    int TotalMessagesImported,
    int TotalDuplicates,
    int TotalAttachmentFailures,
    string? LastSafeError,
    int OpenAlertCount);
