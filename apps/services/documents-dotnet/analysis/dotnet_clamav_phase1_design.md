# Phase 1 — Design and Architecture: Internal ClamAV Scanning

## Service: Documents.NET (port 5006)
## Date: 2026-03-29

---

## 1. Current Upload Flow (Before This Change)

```
Client → POST /documents (multipart)
  → DocumentService.CreateAsync()
    → ValidateMimeType()
    → ScanService.ScanAsync()   ← INLINE, synchronous, blocking
      → IFileScannerProvider.ScanAsync()
        → [NullScannerProvider returns Skipped]
        → [InfectedFileException thrown if infected]
    → _storage.UploadAsync()   ← only reached if scan passed
    → _docs.CreateAsync()      ← ScanStatus = result from scan
    → AuditService.LogAsync()
    → return 201 with scanStatus in response
```

**Problems with inline scanning:**
- Scan blocks the entire HTTP request — for large files this can be 10–30s
- Any scanner timeout = failed upload
- Doesn't support ClamAV over the network (high-latency TCP)
- Infected detection causes 400 at upload time, but file was already partially streamed to storage in some designs
- Not scalable for burst upload loads

---

## 2. Current Scan Flow (Before This Change)

- `ScanService` wraps `IFileScannerProvider`
- `NullScannerProvider` returns `ScanStatus.Skipped` immediately (no real scanning)
- `MockScannerProvider` returns configurable result (`clean`/`infected`/`failed`)
- No actual ClamAV integration existed
- `RequireCleanScanForAccess` was `false` by default — no scan enforcement

---

## 3. New Target Flow (After This Change)

```
Client → POST /documents (multipart)
  → DocumentService.CreateAsync()
    → ValidateMimeType()
    → BuildQuarantineKey()      ← quarantine/tenantId/docTypeId/ts.ext
    → _storage.UploadAsync()   ← file stored in quarantine prefix FIRST
    → _docs.CreateAsync()      ← ScanStatus = Pending  ← NEW
    → _scanOrchestration.EnqueueDocumentScanAsync()  ← async, non-blocking
    → AuditService.LogAsync(DOCUMENT_CREATED, scanStatus=PENDING)
    → return 201 {scanStatus: "PENDING"}  ← IMMEDIATE response

Background (DocumentScanWorker):
  → IScanJobQueue.DequeueAsync()
  → IStorageProvider.DownloadAsync(storageKey)
  → IFileScannerProvider.ScanAsync(stream, fileName)
    → ClamAvFileScannerProvider → TCP → clamd
    → result: {Clean | Infected | Failed}
  → IDocumentRepository.UpdateScanStatusAsync() / IDocumentVersionRepository.UpdateScanStatusAsync()
  → AuditService (SCAN_STARTED, SCAN_CLEAN / SCAN_INFECTED / SCAN_FAILED)
  → If Infected: _storage.DeleteAsync() (purge from quarantine)

Client → GET /documents/:id/url
  → ScanService.EnforceCleanScan()
    → Infected → 403 always
    → Pending  → 403 if RequireCleanScanForAccess=true (default)
    → Failed   → 403 if RequireCleanScanForAccess=true (default)
    → Clean    → 302 to signed URL
```

---

## 4. Architecture Decisions

### 4.1 Quarantine Prefix (not quarantine bucket)

**Decision:** All uploaded files land under `quarantine/{tenantId}/{docTypeId}/{ts}.ext` as the storage key.

**Rationale:**
- Simple, no additional infrastructure required
- Works identically on LocalStorage and S3
- Security gate is enforced at the application layer (scan status check), not at the storage path level
- Storage key semantics change is transparent to the storage provider
- Avoids costly S3 copy/move operations when promoting clean files (files never move)

**Alternative considered:** Separate quarantine bucket, promoted to clean bucket on success. Rejected as over-engineering for v1 — requires cross-bucket copy on S3, complex cleanup on failure, and the quarantine prefix approach achieves the same security posture via application-layer enforcement.

### 4.2 In-Process Queue (not Redis/SQS)

**Decision:** `IScanJobQueue` backed by `System.Threading.Channels.Channel<ScanJob>` (bounded, 1000-capacity).

**Rationale:**
- Zero additional infrastructure for development and small deployments
- Channel is thread-safe and allocation-efficient
- The interface (`IScanJobQueue`) is the extension point — swap to Redis Streams or SQS by replacing the singleton registration in `DependencyInjection.cs`
- Job loss on process restart is acceptable for v1 (re-upload is the recovery path); noted as a known limitation

### 4.3 Background Worker (`BackgroundService`)

**Decision:** `DocumentScanWorker : BackgroundService` registered via `AddHostedService<DocumentScanWorker>()`.

**Rationale:**
- Native ASP.NET Core pattern, no third-party framework required
- Lifecycle managed by the host (started with the app, stopped gracefully via `CancellationToken`)
- Scoped services (repositories) accessed via `IServiceScopeFactory.CreateAsyncScope()`

### 4.4 Fail-Closed Access Enforcement

**Decision:** `RequireCleanScanForAccess` defaults to `true` in `appsettings.json`.

**Rationale:**
- HIPAA-aligned — fail closed for pending/failed scan states
- Infected files are always blocked (unconditional)
- Operators can explicitly loosen to `false` for non-regulated environments
- Matches Node.js service behavior

### 4.5 ClamAV TCP (INSTREAM Protocol)

**Decision:** Direct TCP implementation using `System.Net.Sockets.TcpClient`, no third-party NuGet package.

**Rationale:**
- ClamAV INSTREAM protocol is simple and well-documented
- Avoids package dependency on `nClam` or similar
- Full control over timeout, chunk size, and error handling
- Tested protocol used by dozens of open-source implementations

---

## 5. Clean Architecture Boundaries Preserved

| Layer | Responsibility | Changes |
|-------|---------------|---------|
| Domain | `ScanJob` entity, `IScanJobQueue` interface, `ScanStatusUpdate` | Added `ScanJob`, `IScanJobQueue`; added 2 columns to `Document` |
| Application | `ScanOrchestrationService`, updated `DocumentService` | Added orchestration service; removed inline scan from upload |
| Infrastructure | `ClamAvFileScannerProvider`, `InMemoryScanJobQueue`, storage `DownloadAsync` | All scanner + queue implementations |
| API | `DocumentScanWorker` (Background) | New hosted service only |

The API layer does not call scanner code directly. Domain is provider-agnostic.

---

## 6. Tradeoffs

| Tradeoff | Decision |
|----------|----------|
| Scan latency on upload | Accepted — async model, immediate 201 response |
| Job loss on restart | Accepted for v1 — recoverable via re-upload |
| File access while pending | Blocked by default (`RequireCleanScanForAccess=true`) |
| No file promotion (move) | Accepted — quarantine semantics via DB field |
| ClamAV availability | Fail to `Failed` status, not `Clean` — fail closed |

---

## 7. Risks

| Risk | Mitigation |
|------|-----------|
| clamd unreachable | Scan returns `Failed`; access denied; alerts via structured logs |
| Queue full (1000 jobs) | Writer blocks (bounded channel `Wait` mode); upload stalls under extreme burst |
| Large files in memory during download | `DownloadAsync` returns `Stream` — not buffered into memory; streamed to ClamAV |
| Infected file left in quarantine if purge fails | Logged as ERROR; DB status = `Infected` still blocks access |
| Worker not running (crash) | ASP.NET host restarts it on unhandled exception; structured log CRITICAL |
