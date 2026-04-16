namespace SynqComm.Application.DTOs;

public record AddMessageAttachmentRequest(
    Guid DocumentId,
    string FileName,
    string ContentType,
    long? FileSizeBytes = null);
