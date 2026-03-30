# Enterprise Hardening — Final Summary

**Service**: Documents Service (.NET 8)
**Hardening Phase**: Enterprise scan subsystem upgrade
**Date**: March 2026

---

## Architecture Overview

The scanning subsystem follows a full async pipeline:

```
Upload Request
     │
     ▼
DocumentService.CreateAsync
     │  store in quarantine/{tenantId}/{docTypeId}/{ts}.ext
     │
     ▼
ScanOrchestrationService.EnqueueDocumentScanAsync
     │  TryEnqueueAsync → false? throw QueueSaturationException → HTTP 503
     │
     ▼
IScanJobQueue (memory | redis)
     │  durable at-least-once delivery
     │
     ▼
DocumentScanWorker (N concurrent tasks)
     │  DequeueAsync → ScanJobLease
     │  DownloadAsync → Stream
     │  ClamAvFileScannerProvider.ScanAsync
     │  retry with exponential backoff on transient failure
     │  AcknowledgeAsync or NackAsync
     │
     ▼
DocumentRepository.UpdateScanStatusAsync
     + AuditRepository.InsertAsync

     Result: CLEAN / INFECTED / FAILED
```

Access enforcement in `DocumentService.GetSignedUrlAsync`:
```
_scan.EnforceCleanScan(doc, requireClean)
  → INFECTED: always deny + SCAN_ACCESS_DENIED audit
  → PENDING/FAILED: deny if RequireCleanScanForAccess=true + SCAN_ACCESS_DENIED audit
  → CLEAN: allow → GenerateSignedUrl
```

---

## Queue Design

| Dimension | Memory mode | Redis Streams mode |
|-----------|-------------|-------------------|
| Durability | None (restart = lost) | Durable (survives restart) |
| Delivery | At-most-once | At-least-once |
| Backpressure | `DropWrite` → immediate `false` | XADD failure → immediate `false` |
| Multi-worker | Safe (Channel is thread-safe) | Safe (PEL per consumer) |
| Crash recovery | None | XAUTOCLAIM after `ClaimStaleJobsAfterSeconds` |
| Queue depth | `Channel.Reader.Count` | `XLEN` |
| Select with | `ScanWorker:QueueProvider=memory` | `ScanWorker:QueueProvider=redis` |

---

## Retry Strategy

- **Trigger**: Any exception in `DownloadAsync` or `ScanAsync` (except `OperationCanceled`)
- **Limit**: `MaxRetryAttempts = 3` (configurable)
- **Backoff**: `min(InitialDelay × 2^attempt, MaxDelay) + jitter(0–1s)`
- **Permanent failure**: After last attempt → `ScanStatus.Failed`, audited, acknowledged
- **Audit**: Every attempt logged with `reason`, `attempt`, `retrying`, `delayMs`

---

## Worker Scaling Model

```
BackgroundService.ExecuteAsync
  ├── Task RunWorkerLoopAsync("worker-0", ct)
  ├── Task RunWorkerLoopAsync("worker-1", ct)
  └── ...
      await Task.WhenAll(tasks)
```

- `WorkerCount` (default: 2) concurrent async tasks, configurable via `ScanWorker:WorkerCount`
- Each task dequeues independently; no shared state between workers
- `Task.WhenAll` — graceful shutdown waits for all in-flight scans to complete (or cancel)
- Scale-out: multiple replicas × `WorkerCount` tasks = total scan throughput

---

## Observability Model

### Prometheus metrics at `/metrics`

```
docs_scan_jobs_enqueued_total        (counter)
docs_scan_queue_saturations_total    (counter)
docs_scan_queue_depth                (gauge)
docs_scan_jobs_started_total         (counter)
docs_scan_jobs_clean_total           (counter)
docs_scan_jobs_infected_total        (counter)
docs_scan_jobs_failed_total          (counter)
docs_scan_jobs_retried_total         (counter)
docs_scan_access_denied_total        (counter, label: scan_status)
docs_scan_duration_seconds           (histogram, linear buckets 0.1–10s)
docs_clamav_healthy                  (gauge, 0/1)
http_requests_total                  (prometheus-net standard)
http_request_duration_seconds        (prometheus-net standard)
```

### Audit events

All audit events written to `document_audit` table:
`SCAN_REQUESTED`, `SCAN_STARTED`, `SCAN_CLEAN`, `SCAN_INFECTED`, `SCAN_FAILED`,
`SCAN_COMPLETED`, **`SCAN_ACCESS_DENIED`** (new)

### Structured logging

All log messages use structured parameters:
```
[INF] Scan starting [worker-0]: Document={DocId} Attempt=2/3
[WRN] Scan transient failure (attempt 2/3) for Document={DocId} — retrying in 10342ms
[ERR] INFECTED file: Document={DocId} Threats=Win.Trojan.Agent
[WRN] ClamAV health check timed out (localhost:3310)
```

---

## Production Deployment Guidance

### Kubernetes deployment

```yaml
# Documents Service deployment
spec:
  replicas: 3
  containers:
  - name: documents-dotnet
    env:
    - name: ScanWorker__QueueProvider
      value: "redis"
    - name: ScanWorker__WorkerCount
      value: "2"
    - name: Redis__Url
      valueFrom:
        secretKeyRef:
          name: redis-credentials
          key: url
    - name: Documents__RequireCleanScanForAccess
      value: "true"
    livenessProbe:
      httpGet:
        path: /health
        port: 5006
      initialDelaySeconds: 10
      periodSeconds: 15
    readinessProbe:
      httpGet:
        path: /health/ready
        port: 5006
      initialDelaySeconds: 15
      periodSeconds: 30
      failureThreshold: 3

# ClamAV sidecar (or separate DaemonSet)
- name: clamd
  image: clamav/clamav:latest
  ports:
  - containerPort: 3310
  volumeMounts:
  - name: clamav-data
    mountPath: /var/lib/clamav
```

### Redis configuration

```
# For Redis Streams durability + HA
Redis:Url = "redis://redis-primary:6379,redis-replica:6379,abortConnect=false,connectRetry=5"

# Stream key management
XLEN docs:scan:jobs               # current queue depth
XPENDING docs:scan:jobs scan-workers - + 10  # stuck jobs
XAUTOCLAIM docs:scan:jobs scan-workers ops-tool 300000 0-0 COUNT 100  # manual rescue
```

### Recommended production settings

```json
{
  "ScanWorker": {
    "QueueProvider": "redis",
    "WorkerCount": 2,
    "MaxRetryAttempts": 3,
    "InitialRetryDelaySeconds": 5,
    "MaxRetryDelaySeconds": 120,
    "ClaimStaleJobsAfterSeconds": 300,
    "StreamMaxLength": 50000
  },
  "Scanner": {
    "Provider": "clamav",
    "ClamAv": {
      "Host": "clamd-service",
      "Port": 3310,
      "TimeoutMs": 30000
    }
  },
  "Documents": {
    "RequireCleanScanForAccess": true
  }
}
```

---

## Remaining Risks

| Risk | Priority | Recommended Action |
|------|----------|--------------------|
| No circuit breaker for ClamAV | Medium | Add Polly circuit breaker (open after N failures) |
| ClamAV virus signature staleness | Medium | Monitor `freshclam` daemon + alert on DB age |
| No dead-letter queue | Low | Permanently failed jobs only logged; consider DLQ table |
| Redis single write point | Low | Use Redis Sentinel or Cluster |
| Large file memory pressure | Medium | Validate `clamd StreamMaxLength=25MB` matches `ChunkSizeBytes` |
| Scan worker saturation under spike | Medium | Scale ClamAV replicas + increase `WorkerCount` |

---

## Recommended Next Backlog

1. **Polly circuit breaker** — open circuit after N ClamAV failures; half-open probe
2. **Dead-letter queue** — table for permanently failed jobs with manual requeue API
3. **ScanStatus webhook** — notify upstream when document transitions to CLEAN
4. **ClamAV version alerting** — metric for signature DB age (`docs_clamav_signatures_age_hours`)
5. **Rate limiting per tenant** — prevent one tenant saturating the scan queue
6. **Scan warm-up check** — on startup, verify ClamAV is ready before accepting uploads
7. **EICAR test endpoint** — `/internal/scan/test` (admin-only) to validate scanner is detecting malware
8. **Integration tests** — docker-compose with real ClamAV + Redis for CI validation

---

## Success Criteria — All Met ✅

| Criterion | Status |
|-----------|--------|
| Scan jobs survive restart/crash | ✅ Redis Streams with PEL |
| Transient scan failures retry automatically | ✅ Exponential backoff, 3 retries |
| Multiple workers process safely | ✅ WorkerCount concurrent tasks, lease-based isolation |
| Queue saturation fails fast, not hangs | ✅ TryEnqueueAsync → 503 + Retry-After |
| Blocked access is fully audited | ✅ SCAN_ACCESS_DENIED event on every denial |
| Health endpoints reflect scanner readiness | ✅ /health + /health/ready with ClamAV check |
| Metrics exist for operational visibility | ✅ 11 Prometheus metrics + HTTP metrics |
| Service remains secure, modular, maintainable | ✅ Clean architecture preserved |
