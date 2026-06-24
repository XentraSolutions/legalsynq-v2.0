namespace Liens.Application.DTOs;

public sealed class LegacyDocumentUploadResult
{
    public Guid? DocumentId { get; init; }
    public string Url { get; init; } = string.Empty;
}
