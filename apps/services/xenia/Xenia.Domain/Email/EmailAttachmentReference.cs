namespace Xenia.Domain.Email;

/// <summary>
/// Metadata reference for an attachment discovered during ingestion.
///
/// NEVER stores binary content or Base64-encoded data.
/// The <see cref="DocumentReferenceId"/> points to the document stored by the Documents adapter.
///
/// Dispatch is idempotent: re-dispatching a Pending or Failed attachment is safe.
/// </summary>
public sealed class EmailAttachmentReference
{
    public const int FileNameMaxLength              = 500;
    public const int MimeTypeMaxLength              = 255;
    public const int ProviderAttachmentIdMaxLength  = 1024;
    public const int ContentIdMaxLength             = 500;
    public const int DispositionMaxLength           = 100;
    public const int ErrorCodeMaxLength             = 100;
    public const int SafeErrorSummaryMaxLength      = 500;
    public const int ContentHashMaxLength           = 128;

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid EmailMessageId { get; private set; }

    /// <summary>Provider-assigned attachment identifier (opaque; never logged as a credential).</summary>
    public string? ProviderAttachmentId { get; private set; }

    /// <summary>Document reference returned by the Documents adapter after successful dispatch. Null until dispatched.</summary>
    public Guid? DocumentReferenceId { get; private set; }

    /// <summary>Sanitized, path-safe filename.</summary>
    public string FileName { get; private set; } = string.Empty;
    public string? MimeType { get; private set; }
    public long? SizeBytes { get; private set; }
    public string? ContentHash { get; private set; }

    /// <summary>Whether this is an inline attachment (e.g. embedded image).</summary>
    public bool IsInline { get; private set; }

    /// <summary>Content-ID header value for inline attachments.</summary>
    public string? ContentId { get; private set; }
    public string? Disposition { get; private set; }

    public AttachmentDispatchStatus DispatchStatus { get; private set; } = AttachmentDispatchStatus.Pending;
    public string? ErrorCode { get; private set; }
    public string? SafeErrorSummary { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    private EmailAttachmentReference() { }

    public static EmailAttachmentReference Create(
        Guid tenantId,
        Guid emailMessageId,
        string? providerAttachmentId,
        string fileName,
        string? mimeType,
        long? sizeBytes,
        bool isInline,
        string? contentId)
    {
        return new EmailAttachmentReference
        {
            Id                   = Guid.CreateVersion7(),
            TenantId             = tenantId,
            EmailMessageId       = emailMessageId,
            ProviderAttachmentId = providerAttachmentId,
            FileName             = SanitizeFileName(fileName),
            MimeType             = mimeType,
            SizeBytes            = sizeBytes,
            IsInline             = isInline,
            ContentId            = contentId,
            DispatchStatus       = AttachmentDispatchStatus.Pending,
            CreatedAtUtc         = DateTime.UtcNow,
            UpdatedAtUtc         = DateTime.UtcNow,
        };
    }

    public void MarkDispatched(Guid documentReferenceId, string? contentHash)
    {
        DocumentReferenceId = documentReferenceId;
        ContentHash         = contentHash;
        DispatchStatus      = AttachmentDispatchStatus.Dispatched;
        UpdatedAtUtc        = DateTime.UtcNow;
    }

    public void MarkFailed(string errorCode, string safeErrorSummary)
    {
        ErrorCode        = errorCode;
        SafeErrorSummary = safeErrorSummary;
        DispatchStatus   = AttachmentDispatchStatus.Failed;
        UpdatedAtUtc     = DateTime.UtcNow;
    }

    public void MarkSkipped(string reason)
    {
        SafeErrorSummary = reason;
        DispatchStatus   = AttachmentDispatchStatus.Skipped;
        UpdatedAtUtc     = DateTime.UtcNow;
    }

    /// <summary>Sanitizes a filename: removes path traversal, restricts characters.</summary>
    private static string SanitizeFileName(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return "attachment";
        var name = System.IO.Path.GetFileName(raw);
        if (string.IsNullOrWhiteSpace(name)) return "attachment";
        // Strip any remaining path separators and null bytes
        name = name.Replace('\0', '_').Replace('/', '_').Replace('\\', '_');
        if (name.Length > FileNameMaxLength) name = name[..FileNameMaxLength];
        return name;
    }
}
