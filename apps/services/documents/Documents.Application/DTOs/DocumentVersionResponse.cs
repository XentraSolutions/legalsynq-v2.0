using Documents.Domain.Entities;

namespace Documents.Application.DTOs;

public sealed class DocumentVersionResponse
{
    public Guid     Id               { get; init; }
    public Guid     DocumentId       { get; init; }
    public Guid     TenantId         { get; init; }
    public int      VersionNumber    { get; init; }
    public string   MimeType         { get; init; } = string.Empty;
    public long     FileSizeBytes    { get; init; }
    public string   ScanStatus       { get; init; } = string.Empty;
    public DateTimeOffset? ScanCompletedAt { get; init; }
    public int?     ScanDurationMs   { get; init; }
    public List<string> ScanThreats  { get; init; } = new();
    public string?  ScanEngineVersion { get; init; }
    public string?  Label            { get; init; }
    public bool     IsDeleted        { get; init; }
    public DateTimeOffset? DeletedAt { get; init; }
    public Guid?    DeletedBy        { get; init; }
    public DateTimeOffset UploadedAt { get; init; }
    public Guid     UploadedBy       { get; init; }

    public static DocumentVersionResponse From(DocumentVersion v) => new()
    {
        Id                = v.Id,
        DocumentId        = v.DocumentId,
        TenantId          = v.TenantId,
        VersionNumber     = v.VersionNumber,
        MimeType          = v.MimeType,
        FileSizeBytes     = v.FileSizeBytes,
        ScanStatus        = v.ScanStatus.ToString().ToUpperInvariant(),
        ScanCompletedAt   = ResponseTimestampConverter.Convert(v.ScanCompletedAt),
        ScanDurationMs    = v.ScanDurationMs,
        ScanThreats       = v.ScanThreats,
        ScanEngineVersion = v.ScanEngineVersion,
        Label             = v.Label,
        IsDeleted         = v.IsDeleted,
        DeletedAt         = ResponseTimestampConverter.Convert(v.DeletedAt),
        DeletedBy         = v.DeletedBy,
        UploadedAt        = ResponseTimestampConverter.Convert(v.UploadedAt),
        UploadedBy        = v.UploadedBy,
    };
}
