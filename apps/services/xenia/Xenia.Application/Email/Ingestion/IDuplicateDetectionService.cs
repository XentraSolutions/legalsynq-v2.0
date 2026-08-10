using Xenia.Domain.Email;

namespace Xenia.Application.Email.Ingestion;

/// <summary>
/// Detects whether a normalized message already exists in Xenia.
///
/// Signal priority (in order):
/// 1. ProviderMessageId + TenantId + EmailSourceId (exact provider match — highest confidence)
/// 2. InternetMessageId + TenantId (cross-source deduplication within a tenant)
/// 3. ContentHash + TenantId (fallback for messages without stable IDs)
///
/// Cross-tenant isolation is absolute — no message is shared across tenants.
/// </summary>
public interface IDuplicateDetectionService
{
    /// <summary>
    /// Checks for an existing message matching the given signals.
    /// Returns the existing message's ID if found, null otherwise.
    /// </summary>
    Task<DuplicateCheckResult> CheckAsync(
        Guid tenantId,
        Guid emailSourceId,
        NormalizedMessage message,
        CancellationToken ct = default);
}

public sealed record DuplicateCheckResult
{
    public required bool IsDuplicate { get; init; }
    public Guid? ExistingMessageId { get; init; }
    public string? DuplicateSignal { get; init; }

    public static DuplicateCheckResult NotDuplicate() => new() { IsDuplicate = false };

    public static DuplicateCheckResult Duplicate(Guid existingId, string signal) =>
        new() { IsDuplicate = true, ExistingMessageId = existingId, DuplicateSignal = signal };
}
