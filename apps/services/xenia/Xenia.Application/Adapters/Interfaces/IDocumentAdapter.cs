namespace Xenia.Application.Adapters.Interfaces;

/// <summary>
/// Platform-neutral contract for document service operations needed by Xenia.
/// Xenia modules may need to store or link document records; this adapter abstracts
/// the underlying document service implementation.
/// </summary>
public interface IDocumentAdapter
{
    /// <summary>Whether this adapter is configured for the current environment.</summary>
    bool IsConfigured { get; }

    /// <summary>
    /// Reserves a document record in the platform document service.
    /// Returns a document reference that modules can use to link content.
    /// </summary>
    Task<DocumentReservationResult?> ReserveDocumentAsync(
        Guid tenantId,
        string fileName,
        string contentType,
        CancellationToken ct = default);

    /// <summary>
    /// Uploads an attachment stream to the document service.
    /// The stream is consumed by the adapter; callers must not re-read it after this call.
    /// Returns null if the adapter is unavailable or the upload fails.
    ///
    /// maxSizeBytes is enforced before upload — streams that exceed it are rejected
    /// without buffering the full content.
    /// </summary>
    Task<DocumentUploadResult?> UploadAttachmentStreamAsync(
        Guid tenantId,
        string fileName,
        string contentType,
        Stream contentStream,
        long? maxSizeBytes = null,
        CancellationToken ct = default);

    /// <summary>
    /// Retrieves metadata for a previously stored document.
    /// Returns null when not found or the adapter is unavailable.
    /// </summary>
    Task<DocumentMetadataResult?> GetDocumentMetadataAsync(
        Guid tenantId,
        Guid documentId,
        CancellationToken ct = default);
}

public sealed record DocumentReservationResult(Guid DocumentId, string UploadReference, bool IsAvailable);
public sealed record DocumentUploadResult(Guid DocumentId, string? ContentHash, bool IsAvailable);
public sealed record DocumentMetadataResult(Guid DocumentId, string FileName, string ContentType, long SizeBytes);
