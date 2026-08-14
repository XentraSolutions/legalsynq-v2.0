namespace Documents.Application.DTOs;

public sealed record InternalDocumentMetadataResponse(
    Guid Id,
    Guid TenantId,
    string Status,
    string MimeType,
    string? Sha256,
    bool IsDeleted);