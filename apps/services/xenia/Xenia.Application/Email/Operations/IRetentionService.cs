using Xenia.Domain.Email;

namespace Xenia.Application.Email.Operations;

/// <summary>
/// Executes or simulates tenant-scoped retention policy.
///
/// Rules:
/// - Disabled by default (controlled via EmailOperationalSettings.RetentionEnabled).
/// - Legal hold blocks all deletion when enabled.
/// - Batched to avoid long-lock DB operations.
/// - Dry-run mode produces counts without changes.
/// - Does not delete sync state required for active sources.
/// - Does not delete open alerts.
/// - Audited.
/// - Returns an execution record.
/// </summary>
public interface IRetentionService
{
    Task<EmailRetentionRun> ExecuteAsync(
        Guid tenantId,
        EmailRetentionMode mode,
        Guid? actorId,
        string? correlationId,
        CancellationToken ct = default);

    Task<IReadOnlyList<EmailRetentionRun>> GetHistoryAsync(
        Guid tenantId,
        int limit = 20,
        CancellationToken ct = default);
}
