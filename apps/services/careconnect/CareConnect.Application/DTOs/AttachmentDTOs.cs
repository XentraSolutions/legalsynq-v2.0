namespace CareConnect.Application.DTOs;

public class CreateAttachmentMetadataRequest
{
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public string? ExternalDocumentId { get; set; }
    public string? ExternalStorageProvider { get; set; }
    public string Status { get; set; } = "Pending";
    public string? Notes { get; set; }
}

public class AttachmentMetadataResponse
{
    public Guid Id { get; init; }
    public string FileName { get; init; } = string.Empty;
    public string ContentType { get; init; } = string.Empty;
    public long FileSizeBytes { get; init; }
    public string? ExternalDocumentId { get; init; }
    public string? ExternalStorageProvider { get; init; }
    public string Status { get; init; } = string.Empty;
    public string? Notes { get; init; }
    public DateTime CreatedAtUtc { get; init; }
    public Guid? CreatedByUserId { get; init; }
}
