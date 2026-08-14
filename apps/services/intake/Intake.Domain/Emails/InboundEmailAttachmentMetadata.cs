namespace Intake.Domain.Emails;

public sealed class InboundEmailAttachmentMetadata
{
    public Guid Id { get; set; }
    public Guid InboundEmailId { get; set; }
    public InboundEmail? InboundEmail { get; set; }
    public string? ProviderAttachmentId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string? ContentType { get; set; }
    public string? ContentDisposition { get; set; }
    public string? ContentId { get; set; }
    public long? SizeBytes { get; set; }
    public string? Sha256 { get; set; }
    public bool IsInline { get; set; }
    public int Ordinal { get; set; }
}