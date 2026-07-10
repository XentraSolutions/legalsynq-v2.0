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
    /// Retrieves metadata for a previously stored document.
    /// Returns null when not found or the adapter is unavailable.
    /// </summary>
    Task<DocumentMetadataResult?> GetDocumentMetadataAsync(
        Guid tenantId,
        Guid documentId,
        CancellationToken ct = default);
}

public sealed record DocumentReservationResult(Guid DocumentId, string UploadReference, bool IsAvailable);
public sealed record DocumentMetadataResult(Guid DocumentId, string FileName, string ContentType, long SizeBytes);
