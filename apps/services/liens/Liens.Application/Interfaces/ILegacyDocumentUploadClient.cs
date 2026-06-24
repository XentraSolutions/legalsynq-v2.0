using Liens.Application.DTOs;

namespace Liens.Application.Interfaces;

public interface ILegacyDocumentUploadClient
{
    Task<LegacyDocumentUploadResult> UploadAsync(
        LegacyDocumentUploadRequest request,
        CancellationToken ct = default);
}
