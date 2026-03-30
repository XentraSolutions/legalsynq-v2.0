# Phase 4 — Audit Completion and Observability

## Summary

Added the missing `SCAN_ACCESS_DENIED` audit event, Prometheus metrics for the entire
scan subsystem, and ASP.NET Core health checks for database and ClamAV.

---

## Audit Events (complete coverage)

### New: SCAN_ACCESS_DENIED

Emitted in `DocumentService.GetSignedUrlAsync` whenever `ScanService.EnforceCleanScan`
blocks access to a document:

```csharp
catch (ScanBlockedException)
{
    await _audit.LogAsync(AuditEvent.ScanAccessDenied, ctx, documentId,
        outcome: "DENIED",
        detail: new
        {
            scanStatus       = doc.ScanStatus.ToString(),
            requireCleanScan = _opts.RequireCleanScanForAccess,
        });
    throw;
}
```

**Triggered when**:
- `ScanStatus.Infected` (always, regardless of `RequireCleanScanForAccess`)
- `ScanStatus.Pending` or `ScanStatus.Failed` (when `RequireCleanScanForAccess=true`)

### Complete scan audit event inventory

| Event constant | Emitter | When |
|---------------|---------|------|
| `SCAN_REQUESTED` | `ScanOrchestrationService` | Upload enqueued |
| `SCAN_STARTED` | `DocumentScanWorker` | Worker dequeued job |
| `SCAN_CLEAN` | `DocumentScanWorker` | ClamAV returned clean |
| `SCAN_INFECTED` | `DocumentScanWorker` | Malware detected |
| `SCAN_FAILED` | `DocumentScanWorker` | Transient or permanent failure |
| `SCAN_COMPLETED` | `DocumentScanWorker` | Other status (catch-all) |
| **`SCAN_ACCESS_DENIED`** | **`DocumentService`** | **Access blocked by scan gate** |

---

## Prometheus Metrics

Endpoint: `GET /metrics` (unauthenticated, scrape-friendly)

### Metric inventory

| Metric name | Type | Labels | Description |
|-------------|------|--------|-------------|
| `docs_scan_jobs_enqueued_total` | Counter | — | Jobs enqueued successfully |
| `docs_scan_queue_saturations_total` | Counter | — | Enqueue rejected (queue full) |
| `docs_scan_queue_depth` | Gauge | — | Current pending job count |
| `docs_scan_jobs_started_total` | Counter | — | Jobs started by workers |
| `docs_scan_jobs_clean_total` | Counter | — | Jobs completed CLEAN |
| `docs_scan_jobs_infected_total` | Counter | — | Jobs completed INFECTED |
| `docs_scan_jobs_failed_total` | Counter | — | Jobs permanently FAILED |
| `docs_scan_jobs_retried_total` | Counter | — | Retry attempts (not first attempt) |
| `docs_scan_access_denied_total` | Counter | `scan_status` | Access denied by scan gate |
| `docs_scan_duration_seconds` | Histogram | — | Per-file ClamAV scan duration |
| `docs_clamav_healthy` | Gauge | — | 1=reachable, 0=down |
| HTTP request metrics | Counter/Histogram | `method`, `code`, `controller` | Standard prometheus-net HTTP metrics |

### Implementation

```csharp
public static class ScanMetrics
{
    public static readonly Counter ScanJobsEnqueued = Metrics.CreateCounter(...);
    public static readonly Gauge   ScanQueueDepth   = Metrics.CreateGauge(...);
    // ...
    public static readonly Histogram ScanDurationSeconds = Metrics.CreateHistogram(
        "docs_scan_duration_seconds", "ClamAV scan duration in seconds.",
        new HistogramConfiguration { Buckets = Histogram.LinearBuckets(0.1, 0.5, 20) });
}
```

Updated in:
- `InMemoryScanJobQueue.TryEnqueueAsync` → `ScanJobsEnqueued`, `ScanQueueSaturations`, `ScanQueueDepth`
- `RedisScanJobQueue.TryEnqueueAsync` → same
- `DocumentScanWorker` → `ScanJobsStarted`, `ScanJobsClean`, `ScanJobsInfected`, `ScanJobsFailed`, `ScanJobsRetried`, `ScanDurationSeconds`, `ScanQueueDepth`
- `ClamAvHealthCheck` → `ClamAvHealthy`

---

## Health Checks

### Endpoints

| Endpoint | Tags | Purpose |
|----------|------|---------|
| `GET /health` | `live` | Liveness probe — DB only |
| `GET /health/ready` | `ready` | Readiness probe — DB + ClamAV |

### Response format

```json
{
  "status": "healthy",
  "service": "documents-dotnet",
  "timestamp": "2026-03-29T10:00:00Z",
  "totalDuration": 12.4,
  "checks": [
    {
      "name": "database",
      "status": "healthy",
      "description": "PostgreSQL reachable",
      "duration": 4.1,
      "tags": ["ready", "live"]
    },
    {
      "name": "clamav",
      "status": "degraded",
      "description": "ClamAV unreachable at localhost:3310 — Connection refused",
      "duration": 5001.0,
      "tags": ["ready"]
    }
  ]
}
```

**Status codes**:
- `200` — Healthy or Degraded (service running, some dependencies optional)
- `503` — Unhealthy (DB unreachable = service not ready)

### Health check implementations

**`DatabaseHealthCheck`**: Uses `IServiceScopeFactory` to get `DocsDbContext` and calls
`db.Database.CanConnectAsync()`. Registered with `failureStatus: Unhealthy` and tags `["ready", "live"]`.

**`ClamAvHealthCheck`**: Connects to clamd via TCP, sends `zPING\0` (null-terminated),
expects `PONG` response. Registered with `failureStatus: Degraded` and tag `["ready"]`.
ClamAV degraded does NOT block the liveness probe — only readiness.
Updates `docs_clamav_healthy` gauge after each check.

---

## Files Changed

| File | Change |
|------|--------|
| `Documents.Application/Services/DocumentService.cs` | Added `SCAN_ACCESS_DENIED` audit on access denied |
| `Documents.Infrastructure/Observability/ScanMetrics.cs` | NEW — all Prometheus metrics |
| `Documents.Infrastructure/Health/ClamAvHealthCheck.cs` | NEW — TCP PING/PONG health check |
| `Documents.Infrastructure/Health/DatabaseHealthCheck.cs` | NEW — EF Core CanConnect check |
| `Documents.Infrastructure/DependencyInjection.cs` | Register health checks |
| `Documents.Api/Endpoints/HealthEndpoints.cs` | UPDATED — ASP.NET Core MapHealthChecks |
| `Documents.Api/Program.cs` | Added `app.MapMetrics("/metrics")` + `UseHttpMetrics` |
| `Documents.Api/Documents.Api.csproj` | Added `prometheus-net.AspNetCore 8.2.1` |
| `Documents.Infrastructure/Documents.Infrastructure.csproj` | Added `prometheus-net 8.2.1` |

---

## Recommended Alerting Rules (Prometheus/Alertmanager)

```yaml
- alert: ScanQueueSaturated
  expr: increase(docs_scan_queue_saturations_total[5m]) > 10
  severity: warning

- alert: ScanFailureRate
  expr: rate(docs_scan_jobs_failed_total[10m]) > 0.1
  severity: critical

- alert: ClamAvDown
  expr: docs_clamav_healthy == 0
  for: 2m
  severity: critical

- alert: HighScanQueueDepth
  expr: docs_scan_queue_depth > 800
  severity: warning
```

---

## Verification Steps

1. Access a document with `PENDING` scan status → check audit trail for `SCAN_ACCESS_DENIED`
2. Scrape `GET /metrics` → verify all `docs_scan_*` metrics are present
3. `GET /health` → should return `200 { "status": "healthy" }` (no ClamAV check)
4. `GET /health/ready` → should show both `database` and `clamav` checks
5. Stop ClamAV → `/health/ready` returns `200 { "status": "degraded" }`, `/health` still `200 { "status": "healthy" }`
6. Stop PostgreSQL → `/health` returns `503`, `/health/ready` returns `503`
