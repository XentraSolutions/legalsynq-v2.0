namespace Intake.Application.Emails;

public sealed class EmailCaptureOptions
{
    public long MaxInboundMessageBytes { get; set; } = 25 * 1024 * 1024;
    public long MaxTextBodyBytes { get; set; } = 4 * 1024 * 1024;
    public long MaxHtmlBodyBytes { get; set; } = 8 * 1024 * 1024;
    public long MaxHeaderBytes { get; set; } = 64 * 1024;
    public int MaxAttachmentMetadataCount { get; set; } = 100;
    public int MaxRecipientsPerMessage { get; set; } = 100;
    public int MaxSubjectLength { get; set; } = 998;
}