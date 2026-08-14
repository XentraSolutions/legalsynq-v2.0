using Intake.Application.Snapshot;

namespace Intake.Application.Artifacts;

public interface IIntakeDocumentsClient
{
    Task<DocumentMetadataResult> GetMetadataAsync(
        Guid tenantId,
        Guid documentId,
        CancellationToken cancellationToken);

    Task<DocumentsLookupResult> FindByReferenceAsync(
        Guid tenantId,
        string referenceId,
        string referenceType,
        CancellationToken cancellationToken);

    Task<DocumentsUploadResult> UploadAsync(
        Stream content,
        string fileName,
        string contentType,
        long fileSizeBytes,
        Guid tenantId,
        string title,
        string description,
        string productId,
        Guid documentTypeId,
        string referenceId,
        string referenceType,
        CancellationToken cancellationToken);
}