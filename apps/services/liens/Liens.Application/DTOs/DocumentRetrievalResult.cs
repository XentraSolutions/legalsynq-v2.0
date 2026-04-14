namespace Liens.Application.DTOs;

public sealed class DocumentRetrievalResult
{
    public required Stream Content { get; init; }
    public required string ContentType { get; init; }
    public required string FileName { get; init; }
    public long? ContentLength { get; init; }
}
