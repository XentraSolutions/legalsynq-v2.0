namespace Intake.Application.Artifacts;

public sealed class EmailArtifactProcessingOptions
{
    public const string SectionName = "Intake:EmailArtifacts";

    public int MaxMimeInputBytes { get; set; } = 16 * 1024 * 1024;
    public int MaxArtifactBytes { get; set; } = 25 * 1024 * 1024;
    public int MaxArtifactsPerEmail { get; set; } = 100;
    public long MaxTotalArtifactBytesPerEmail { get; set; } = 100 * 1024 * 1024;
    public int MaxMimeDepth { get; set; } = 20;
    public int MaxFileNameLength { get; set; } = 240;
    public int MaxManualFiles { get; set; } = 100;
    public int MaxManualFileBytes { get; set; } = 25 * 1024 * 1024;
    public long MaxTotalManualFileBytes { get; set; } = 100 * 1024 * 1024;
    public int DocumentsServiceTimeoutSeconds { get; set; } = 60;
    public string DocumentsServiceBaseUrl { get; set; } = "http://localhost:5006";
    public string DocumentsServiceProductId { get; set; } = "SYNQ_INTAKE";
    public string? DocumentsServiceDocumentTypeId { get; set; }
}