# Phase 3 — Internal Scan Job Workflow

## Service: Documents.NET (port 5006)
## Date: 2026-03-29

---

## 1. Files Implemented

| File | Description |
|------|-------------|
| `Documents.Domain/Entities/ScanJob.cs` | In-process scan job value object |
| `Documents.Domain/Interfaces/IScanJobQueue.cs` | Port interface for scan queue |
| `Documents.Infrastructure/Scanner/InMemoryScanJobQueue.cs` | Channel-based implementation |
| `Documents.Application/Services/ScanOrchestrationService.cs` | Application-layer enqueue coordinator |
| `Documents.Api/Background/DocumentScanWorker.cs` | BackgroundService consumer |
| `Documents.Api/Program.cs` | Added `AddHostedService<DocumentScanWorker>()` |
| `Documents.Infrastructure/DependencyInjection.cs` | Registered `IScanJobQueue`, `ScanOrchestrationService` |

---

## 2. ScanJob Entity

```csharp
public sealed class ScanJob
{
    public required Guid    DocumentId  { get; init; }
    public required Guid    TenantId    { get; init; }
    public Guid?    VersionId   { get; init; }   // null = document, non-null = version
    public required string  StorageKey  { get; init; }
    public required string  FileName    { get; init; }
    public required string  MimeType    { get; init; }
    public DateTime EnqueuedAt { get; init; }
    public int      AttemptCount { get; set; }
}
```

- `VersionId == null` → scan the document record itself
- `VersionId != null` → scan a specific version record
- `StorageKey` is used by the worker to download the file from storage

---

## 3. IScanJobQueue Interface

```csharp
public interface IScanJobQueue
{
    ValueTask EnqueueAsync(ScanJob job, CancellationToken ct = default);
    ValueTask<ScanJob?> DequeueAsync(CancellationToken ct = default);
    int Count { get; }
}
```

This is a domain port. The production implementation (`InMemoryScanJobQueue`) uses `System.Threading.Channels`. Future implementations can back this with Redis Streams, AWS SQS, or Azure Service Bus with no structural changes to the worker or orchestration service.

---

## 4. InMemoryScanJobQueue

Uses a **bounded `Channel<ScanJob>`**:

```csharp
var opts = new BoundedChannelOptions(capacity: 1000)
{
    FullMode     = BoundedChannelFullMode.Wait,
    SingleReader = true,
    SingleWriter = false,
};
```

- **Bounded:** Prevents unbounded memory growth under burst load
- **Wait mode:** Upload handler blocks (not drops) when queue is full — backpressure
- **SingleReader:** Optimized for the single `DocumentScanWorker` consumer
- **SingleWriter = false:** Thread-safe for concurrent API handlers

**Registered as singleton** in DI — shared between all scoped `ScanOrchestrationService` instances and the singleton `DocumentScanWorker`.

---

## 5. ScanOrchestrationService

Application-layer coordinator. Responsibilities:
1. Build `ScanJob` from domain entities
2. Enqueue to `IScanJobQueue`
3. Emit `SCAN_REQUESTED` audit event

```csharp
// For document uploads
await _scanOrchestration.EnqueueDocumentScanAsync(created, fileName, mimeType, ctx, ct);

// For version uploads
await _scanOrchestration.EnqueueVersionScanAsync(created, doc, fileName, mimeType, ctx, ct);
```

Registered as **Scoped** (same lifetime as `DocumentService`) to allow access to the per-request `AuditService`.

---

## 6. DocumentScanWorker (BackgroundService)

### Lifecycle

```
Host starts → DocumentScanWorker.StartAsync()
           → ExecuteAsync() begins loop
             → DequeueAsync() — blocks until job available
             → ProcessJobAsync(job)
             → Loop
Host stops → CancellationToken cancelled
           → DequeueAsync() returns null
           → Loop exits cleanly
```

### ProcessJobAsync Flow

```
1. Audit SCAN_STARTED
2. DownloadAsync(storageKey) → Stream
   → If storage error: UpdateScanStatus(Failed), Audit SCAN_FAILED, return
3. ScanAsync(stream, fileName) → ScanResult
4. UpdateScanStatusAsync(id/versionId, tenantId, update) via repository
5. Switch on result.Status:
   Clean    → Audit SCAN_CLEAN
   Infected → Audit SCAN_INFECTED + DeleteAsync(storageKey) (purge)
   Failed   → Audit SCAN_FAILED
   Other    → Audit SCAN_COMPLETED
```

### Scoped Services in Background Worker

Repositories (`IDocumentRepository`, `IDocumentVersionRepository`, `IAuditRepository`) are scoped services. The worker uses `IServiceScopeFactory.CreateAsyncScope()` to create a scope per operation:

```csharp
await using var scope = _scopes.CreateAsyncScope();
var docRepo = scope.ServiceProvider.GetRequiredService<IDocumentRepository>();
```

This pattern is the standard ASP.NET Core approach for using scoped services in singleton/background workers.

### Singleton dependencies (injected directly)

- `IScanJobQueue` — singleton
- `IStorageProvider` — singleton
- `IFileScannerProvider` — singleton
- `IServiceScopeFactory` — singleton

---

## 7. Async Scan Timeline

```
t=0   POST /documents ─────────────────────────────────→ 201 {scanStatus: "PENDING"}
t=0   Job enqueued in Channel
t=~0  Worker dequeues job
t=1   SCAN_STARTED audit
t=2   File downloaded from quarantine storage
t=3   ClamAV TCP scan (duration: depends on file size + clamd speed)
t=4   UpdateScanStatusAsync (CLEAN / INFECTED / FAILED)
t=4   SCAN_CLEAN / SCAN_INFECTED / SCAN_FAILED audit

t=?   GET /documents/:id/url
        → ScanStatus=CLEAN  → 302 signed URL
        → ScanStatus=PENDING → 403 Access denied: scan status is PENDING
```

---

## 8. Known Limitations (v1)

| Limitation | Notes |
|-----------|-------|
| No persistence | Jobs lost on process restart — re-upload required |
| No retry logic | Failed jobs are not re-queued; retry = re-upload |
| Single worker | One concurrent scan at a time; sufficient for most deployments |
| No dead-letter queue | Permanent failures drop the job; audit trail shows SCAN_FAILED |

### Upgrade path to Redis Streams

1. Implement `RedisStreamScanJobQueue : IScanJobQueue`
2. Change DI registration: `services.AddSingleton<IScanJobQueue, RedisStreamScanJobQueue>()`
3. `DocumentScanWorker` requires zero changes
4. Optional: change `DocumentScanWorker` to `MaxConcurrency > 1` by running multiple loops

---

## 9. Configuration

```json
"ScanWorker": {
  "QueueCapacity": 1000
}
```

`QueueCapacity` is currently hardcoded at 1000 in `InMemoryScanJobQueue`. Exposing it via options is a minor follow-up task.
