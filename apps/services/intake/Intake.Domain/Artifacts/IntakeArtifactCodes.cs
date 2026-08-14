namespace Intake.Domain.Artifacts;

public static class IntakeArtifactTypes
{
    public const string Attachment = "EMAIL_ATTACHMENT";
    public const string ManualFile = "MANUAL_FILE";
    public const string TextBody = "EMAIL_BODY_TEXT";
    public const string HtmlBody = "EMAIL_BODY_HTML";
}

public static class IntakeArtifactRoles
{
    public const string Attachment = "ATTACHMENT";
    public const string InlineAttachment = "INLINE_ATTACHMENT";
    public const string TextBody = "TEXT_BODY";
    public const string HtmlBody = "HTML_BODY";
}

public static class IntakeArtifactProcessingStatuses
{
    public const string Pending = "PENDING";
    public const string Processing = "PROCESSING";
    public const string Completed = "COMPLETED";
    public const string Skipped = "SKIPPED";
    public const string Failed = "FAILED";
}

public static class InboundEmailArtifactProcessingStatuses
{
    public const string NotStarted = "NOT_STARTED";
    public const string InProgress = "IN_PROGRESS";
    public const string Completed = "COMPLETED";
    public const string Partial = "PARTIAL";
    public const string Failed = "FAILED";
}

public static class IntakeArtifactFailureCodes
{
    public const string RawMessageUnavailable = "RAW_MESSAGE_UNAVAILABLE";
    public const string MimeInputTooLarge = "MIME_INPUT_TOO_LARGE";
    public const string MimeParseFailed = "MIME_PARSE_FAILED";
    public const string MimeDepthExceeded = "MIME_DEPTH_EXCEEDED";
    public const string ArtifactCountExceeded = "ARTIFACT_COUNT_EXCEEDED";
    public const string ArtifactBytesExceeded = "ARTIFACT_BYTES_EXCEEDED";
    public const string AttachmentNotFound = "ATTACHMENT_NOT_FOUND";
    public const string AttachmentMetadataMismatch = "ATTACHMENT_METADATA_MISMATCH";
    public const string AttachmentHashMismatch = "ATTACHMENT_HASH_MISMATCH";
    public const string AttachmentSizeMismatch = "ATTACHMENT_SIZE_MISMATCH";
    public const string UnsupportedContentType = "UNSUPPORTED_CONTENT_TYPE";
    public const string InvalidFileName = "INVALID_FILE_NAME";
    public const string DocumentsServiceUnavailable = "DOCUMENTS_SERVICE_UNAVAILABLE";
    public const string DocumentsUploadRejected = "DOCUMENTS_UPLOAD_REJECTED";
    public const string DocumentsResponseInvalid = "DOCUMENTS_RESPONSE_INVALID";
    public const string ProcessingConfigurationInvalid = "PROCESSING_CONFIGURATION_INVALID";
    public const string ManualFileRequiredForRetry = "MANUAL_FILE_REQUIRED_FOR_RETRY";
    public const string ManualFileHashMismatch = "MANUAL_FILE_HASH_MISMATCH";
}