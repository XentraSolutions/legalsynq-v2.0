using Xenia.Domain.Email;

namespace Xenia.Application.Email.Ingestion;

/// <summary>
/// Persists a batch of normalized messages for a single ingestion run.
///
/// Staged process per message:
/// 1. Insert EmailMessage + Recipients atomically.
/// 2. Commit. Cursor must only advance after this commit.
/// 3. Persist EmailAttachmentReference stubs (Pending status).
/// 4. Return attachment stubs to caller for async dispatch.
///
/// No binary content is ever stored here.
/// </summary>
public interface IMessagePersistenceService
{
    /// <summary>
    /// Persists a single normalized message and its recipients.
    /// Returns the created or updated message ID and any attachment stubs.
    /// Thread-safe for different (tenantId, sourceId) pairs; not safe for concurrent calls
    /// with the same message.
    /// </summary>
    Task<MessagePersistenceResult> PersistMessageAsync(
        Guid tenantId,
        Guid emailSourceId,
        EmailProviderType providerType,
        NormalizedMessage message,
        Guid runId,
        DuplicateCheckResult duplicateCheck,
        CancellationToken ct = default);
}

public sealed record MessagePersistenceResult
{
    public required bool Success { get; init; }
    public Guid? MessageId { get; init; }
    public required MessageImportStatus ImportStatus { get; init; }

    /// <summary>Attachment stubs persisted (Pending dispatch) — may be empty.</summary>
    public IReadOnlyList<Guid> AttachmentReferenceIds { get; init; } = [];

    public string? ErrorCode { get; init; }
    public string? SafeErrorSummary { get; init; }
}
