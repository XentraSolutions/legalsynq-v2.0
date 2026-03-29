using Documents.Domain.Entities;
using Documents.Domain.Enums;
using Documents.Domain.Interfaces;
using Documents.Application.Models;
using Microsoft.Extensions.Logging;

namespace Documents.Application.Services;

/// <summary>
/// Application-layer coordinator for the asynchronous scan workflow.
/// Called by DocumentService after upload to enqueue a scan job.
/// The actual scan is performed by DocumentScanWorker (background worker).
/// </summary>
public sealed class ScanOrchestrationService
{
    private readonly IScanJobQueue                       _queue;
    private readonly AuditService                        _audit;
    private readonly ILogger<ScanOrchestrationService>  _log;

    public ScanOrchestrationService(
        IScanJobQueue                       queue,
        AuditService                        audit,
        ILogger<ScanOrchestrationService>   log)
    {
        _queue = queue;
        _audit = audit;
        _log   = log;
    }

    /// <summary>
    /// Enqueue an antivirus scan for a newly uploaded document (no version).
    /// </summary>
    public async Task EnqueueDocumentScanAsync(
        Document       doc,
        string         fileName,
        string         mimeType,
        RequestContext ctx,
        CancellationToken ct = default)
    {
        var job = new ScanJob
        {
            DocumentId = doc.Id,
            TenantId   = doc.TenantId,
            VersionId  = null,
            StorageKey = doc.StorageKey,
            FileName   = fileName,
            MimeType   = mimeType,
        };

        await _queue.EnqueueAsync(job, ct);

        _log.LogInformation(
            "Scan enqueued: Document={DocId} Tenant={TenantId} File={File}",
            doc.Id, doc.TenantId, fileName);

        await _audit.LogAsync(
            AuditEvent.ScanRequested, ctx, doc.Id,
            detail: new { fileName, mimeType, queueDepth = _queue.Count });
    }

    /// <summary>
    /// Enqueue an antivirus scan for a newly uploaded document version.
    /// </summary>
    public async Task EnqueueVersionScanAsync(
        DocumentVersion version,
        Document        parentDoc,
        string          fileName,
        string          mimeType,
        RequestContext  ctx,
        CancellationToken ct = default)
    {
        var job = new ScanJob
        {
            DocumentId = parentDoc.Id,
            TenantId   = parentDoc.TenantId,
            VersionId  = version.Id,
            StorageKey = version.StorageKey,
            FileName   = fileName,
            MimeType   = mimeType,
        };

        await _queue.EnqueueAsync(job, ct);

        _log.LogInformation(
            "Scan enqueued: Document={DocId} Version={VersionId} Tenant={TenantId} File={File}",
            parentDoc.Id, version.Id, parentDoc.TenantId, fileName);

        await _audit.LogAsync(
            AuditEvent.ScanRequested, ctx, parentDoc.Id,
            detail: new { versionId = version.Id, fileName, mimeType, queueDepth = _queue.Count });
    }
}
