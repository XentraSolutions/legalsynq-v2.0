namespace Liens.Application.DTOs;

public sealed class LegacyDocumentUploadRequest
{
    public Guid TenantId { get; init; }
    public Guid ActingUserId { get; init; }
    public Guid ReferenceId { get; init; }
    public string ReferenceType { get; init; } = string.Empty;
    public Guid DocumentTypeId { get; init; }
    public string Title { get; init; } = string.Empty;
    public string? Description { get; init; }
    public Stream Content { get; init; } = Stream.Null;
    public string FileName { get; init; } = string.Empty;
    public string ContentType { get; init; } = "application/octet-stream";
    public long Length { get; init; }
}
