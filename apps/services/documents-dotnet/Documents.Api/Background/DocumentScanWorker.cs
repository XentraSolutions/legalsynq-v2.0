using Documents.Application.Services;
using Documents.Domain.Entities;
using Documents.Domain.Enums;
using Documents.Domain.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Documents.Api.Background;

/// <summary>
/// Long-running background service that dequeues scan jobs from IScanJobQueue,
/// downloads the file from storage, invokes the IFileScannerProvider, then
/// updates the document or version scan status in the database.
///
/// One worker instance is sufficient for in-process queues. For multi-replica
/// deployments, replace IScanJobQueue with a distributed queue (Redis Streams /
/// AWS SQS) — this class needs no structural change.
/// </summary>
public sealed class DocumentScanWorker : BackgroundService
{
    private readonly IScanJobQueue         _queue;
    private readonly IStorageProvider      _storage;
    private readonly IFileScannerProvider  _scanner;
    private readonly IServiceScopeFactory  _scopes;
    private readonly ILogger<DocumentScanWorker> _log;

    public DocumentScanWorker(
        IScanJobQueue              queue,
        IStorageProvider           storage,
        IFileScannerProvider       scanner,
        IServiceScopeFactory       scopes,
        ILogger<DocumentScanWorker> log)
    {
        _queue   = queue;
        _storage = storage;
        _scanner = scanner;
        _scopes  = scopes;
        _log     = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _log.LogInformation("DocumentScanWorker started — provider={Provider}", _scanner.ProviderName);

        while (!stoppingToken.IsCancellationRequested)
        {
            var job = await _queue.DequeueAsync(stoppingToken);
            if (job is null) break;

            try
            {
                await ProcessJobAsync(job, stoppingToken);
            }
            catch (Exception ex)
            {
                _log.LogError(ex,
                    "Unhandled error scanning Document={DocId} Version={VersionId}",
                    job.DocumentId, job.VersionId);
            }
        }

        _log.LogInformation("DocumentScanWorker stopped");
    }

    // ── Core scan processing ──────────────────────────────────────────────────

    private async Task ProcessJobAsync(ScanJob job, CancellationToken ct)
    {
        _log.LogInformation(
            "Scan starting: Document={DocId} Version={VersionId} File={File} Attempt={Attempt}",
            job.DocumentId, job.VersionId, job.FileName, job.AttemptCount + 1);

        // 1. Audit: scan started
        await AuditScanEventAsync(job, AuditEvent.ScanStarted,
            new { job.FileName, job.StorageKey, attempt = job.AttemptCount + 1 }, ct);

        // 2. Download file stream from quarantine storage
        Stream fileStream;
        try
        {
            fileStream = await _storage.DownloadAsync(job.StorageKey, ct);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Failed to download file for scanning: {StorageKey}", job.StorageKey);
            await SetScanStatusAsync(job, ScanStatus.Failed, new(), null, null, ct);
            await AuditScanEventAsync(job, AuditEvent.ScanFailed,
                new { reason = "storage_download_error", error = ex.Message }, ct);
            return;
        }

        // 3. Run antivirus scan
        ScanResult result;
        await using (fileStream)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            result = await _scanner.ScanAsync(fileStream, job.FileName, ct);
            sw.Stop();

            _log.LogInformation(
                "Scan result: Document={DocId} Status={Status} Threats={Count} Duration={Ms}ms",
                job.DocumentId, result.Status, result.Threats.Count, (int)sw.ElapsedMilliseconds);
        }

        // 4. Persist scan result to database
        await SetScanStatusAsync(
            job,
            result.Status,
            result.Threats,
            result.DurationMs,
            result.EngineVersion,
            ct);

        // 5. Post-scan actions
        switch (result.Status)
        {
            case ScanStatus.Clean:
                await AuditScanEventAsync(job, AuditEvent.ScanClean,
                    new { result.Threats.Count, result.DurationMs, result.EngineVersion }, ct);
                break;

            case ScanStatus.Infected:
                _log.LogWarning(
                    "INFECTED file detected: Document={DocId} Version={VersionId} Threats={Threats}",
                    job.DocumentId, job.VersionId, result.Threats);

                await AuditScanEventAsync(job, AuditEvent.ScanInfected,
                    new { result.Threats, result.EngineVersion }, ct);

                // Remove infected file from quarantine storage (fail-safe)
                await PurgeInfectedFileAsync(job, ct);
                break;

            case ScanStatus.Failed:
                await AuditScanEventAsync(job, AuditEvent.ScanFailed,
                    new { result.DurationMs, result.EngineVersion }, ct);
                break;

            default:
                await AuditScanEventAsync(job, AuditEvent.ScanCompleted,
                    new { status = result.Status.ToString() }, ct);
                break;
        }
    }

    // ── Database update (uses scoped services) ────────────────────────────────

    private async Task SetScanStatusAsync(
        ScanJob           job,
        ScanStatus        status,
        List<string>      threats,
        int?              durationMs,
        string?           engineVersion,
        CancellationToken ct)
    {
        var update = new ScanStatusUpdate
        {
            ScanStatus        = status,
            ScanCompletedAt   = DateTime.UtcNow,
            ScanDurationMs    = durationMs,
            ScanThreats       = threats,
            ScanEngineVersion = engineVersion,
        };

        await using var scope = _scopes.CreateAsyncScope();

        if (job.VersionId.HasValue)
        {
            var versionRepo = scope.ServiceProvider
                .GetRequiredService<IDocumentVersionRepository>();

            await versionRepo.UpdateScanStatusAsync(job.VersionId.Value, job.TenantId, update, ct);
        }
        else
        {
            var docRepo = scope.ServiceProvider
                .GetRequiredService<IDocumentRepository>();

            await docRepo.UpdateScanStatusAsync(job.DocumentId, job.TenantId, update, ct);
        }
    }

    // ── Quarantine purge ──────────────────────────────────────────────────────

    private async Task PurgeInfectedFileAsync(ScanJob job, CancellationToken ct)
    {
        try
        {
            await _storage.DeleteAsync(job.StorageKey, ct);
            _log.LogWarning("Purged infected file from quarantine: {Key}", job.StorageKey);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Failed to purge infected file: {Key}", job.StorageKey);
        }
    }

    // ── Audit helper (fire-and-forget failures tolerated) ─────────────────────

    private async Task AuditScanEventAsync(
        ScanJob           job,
        string            eventType,
        object            detail,
        CancellationToken ct)
    {
        try
        {
            await using var scope = _scopes.CreateAsyncScope();
            var auditRepo = scope.ServiceProvider.GetRequiredService<IAuditRepository>();

            var audit = new DocumentAudit
            {
                Id         = Guid.NewGuid(),
                TenantId   = job.TenantId,
                DocumentId = job.DocumentId,
                Event      = eventType,
                ActorId    = null,  // system background worker — no user context
                Outcome    = "SUCCESS",
                Detail     = System.Text.Json.JsonSerializer.Serialize(detail),
                OccurredAt = DateTime.UtcNow,
            };

            await auditRepo.InsertAsync(audit, ct);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Failed to write audit event {Event} for Document={DocId}",
                eventType, job.DocumentId);
        }
    }
}
