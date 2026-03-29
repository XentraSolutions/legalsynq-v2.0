# Phase 5 — Audit, Configuration, and Parity Review

## Service: Documents.NET (port 5006)
## Date: 2026-03-29

---

## 1. Audit Events — Full Scan Lifecycle

All scan lifecycle events are captured in `document_audits`.

### 1.1 Audit Event Registry

| Constant | Value | Actor | When |
|----------|-------|-------|------|
| `AuditEvent.ScanRequested` | `SCAN_REQUESTED` | API user | On upload, when job is enqueued |
| `AuditEvent.ScanStarted` | `SCAN_STARTED` | `scan-worker` (null ActorId) | Worker dequeues and starts scan |
| `AuditEvent.ScanClean` | `SCAN_CLEAN` | `scan-worker` | Scanner returns Clean |
| `AuditEvent.ScanCompleted` | `SCAN_COMPLETED` | `scan-worker` | Scan completed (non-clean catch-all) |
| `AuditEvent.ScanFailed` | `SCAN_FAILED` | `scan-worker` | Scanner error / storage error |
| `AuditEvent.ScanInfected` | `SCAN_INFECTED` | `scan-worker` | Scanner detects malware |
| `AuditEvent.ScanAccessDenied` | `SCAN_ACCESS_DENIED` | API user | Access denied due to scan status |

### 1.2 Audit Detail Payloads

**SCAN_REQUESTED** (by `ScanOrchestrationService`):
```json
{ "fileName": "contract.pdf", "mimeType": "application/pdf", "queueDepth": 3 }
```

**SCAN_STARTED** (by `DocumentScanWorker`):
```json
{ "fileName": "contract.pdf", "storageKey": "quarantine/...", "attempt": 1 }
```

**SCAN_CLEAN**:
```json
{ "Count": 0, "DurationMs": 142, "EngineVersion": "clamav/localhost:3310" }
```

**SCAN_INFECTED**:
```json
{ "Threats": ["Eicar-Test-Signature"], "EngineVersion": "clamav/localhost:3310" }
```

**SCAN_FAILED**:
```json
{ "DurationMs": 30001, "EngineVersion": "clamav/localhost:3310" }
// or: { "reason": "storage_download_error", "error": "FileNotFoundException: ..." }
```

### 1.3 Known Audit Gap

`ScanAccessDenied` is NOT currently emitted from `ScanService.EnforceCleanScan()`. This is a follow-up task. The constant and schema support exist. Implementation:

```csharp
// In DocumentService.GetSignedUrlAsync() after calling EnforceCleanScan():
catch (ScanBlockedException)
{
    await _audit.LogAsync(AuditEvent.ScanAccessDenied, ctx, documentId,
        outcome: "DENIED",
        detail: new { scanStatus = doc.ScanStatus.ToString() });
    throw;
}
```

---

## 2. Configuration Reference

### Full `appsettings.json` Scanner Section

```json
{
  "Scanner": {
    "Provider": "none",
    "ClamAv": {
      "Host":           "localhost",
      "Port":           3310,
      "TimeoutMs":      30000,
      "ChunkSizeBytes": 2097152
    },
    "Mock": {
      "MockResult": "clean"
    }
  },
  "ScanWorker": {
    "QueueCapacity": 1000
  },
  "Documents": {
    "RequireCleanScanForAccess": true,
    "SignedUrlTtlSeconds": 30
  }
}
```

### Environment Variable Overrides

```bash
# Use ClamAV scanner
Scanner__Provider=clamav
Scanner__ClamAv__Host=clamd.internal
Scanner__ClamAv__Port=3310

# Relax access enforcement (non-production only)
Documents__RequireCleanScanForAccess=false

# Mock for testing
Scanner__Provider=mock
Scanner__Mock__MockResult=infected
```

---

## 3. Run Instructions

### 3.1 Development (no real scanner)

```bash
# Start with mock scanner (clean)
Scanner__Provider=mock Scanner__Mock__MockResult=clean \
  dotnet run --project Documents.Api/Documents.Api.csproj
```

### 3.2 Development with ClamAV via Docker

```bash
# 1. Start clamd
docker run -d --name clamd -p 3310:3310 clamav/clamav:latest

# 2. Wait for freshclam to complete (first run downloads signatures ~250 MB)
docker logs -f clamd  # watch for "socket found, clamd started"

# 3. Start service with clamav scanner
Scanner__Provider=clamav \
  dotnet run --project Documents.Api/Documents.Api.csproj
```

### 3.3 Testing EICAR Virus Detection

```bash
# Upload EICAR test file (standard AV test, not a real virus)
curl -X POST http://localhost:5006/documents \
  -H "Authorization: Bearer $TOKEN" \
  -F "file=@eicar.com" \
  -F "tenantId=..." \
  ...

# Response: 201 { scanStatus: "PENDING" }
# Background: worker detects INFECTED, purges file, updates DB
# GET /documents/:id → { scanStatus: "INFECTED" }
# GET /documents/:id/url → 403 SCAN_BLOCKED
```

---

## 4. Dev/Test Workflow

### Recommended Dev Setup

1. Run with `Scanner:Provider=mock`
2. Set `Scanner:Mock:MockResult=clean` for normal development
3. To test access enforcement: set `MockResult=infected` and verify 403 responses
4. To test pending gating: temporarily skip scan worker startup (not currently supported via config; requires code change to worker)

### Integration Test Sequence (ClamAV required)

```
1. Upload PDF → 201 { scanStatus: "PENDING" }
2. Poll GET /documents/:id until scanStatus != "PENDING"
3. Assert scanStatus == "CLEAN"
4. GET /documents/:id/url → 302
5. Upload EICAR → 201 { scanStatus: "PENDING" }
6. Poll → scanStatus == "INFECTED"
7. GET /documents/:id/url → 403
8. GET /documents/:id (metadata) → still accessible (metadata, not file)
```

---

## 5. Parity with Node.js Documents Service

### 5.1 Behavior Parity

| Feature | Node.js Service | .NET Service | Parity |
|---------|----------------|--------------|--------|
| Async scan (queue) | ✅ Bull queue | ✅ Channel queue | ✅ Equivalent |
| Quarantine prefix | ✅ `quarantine/` key | ✅ `quarantine/` key | ✅ Match |
| Pending blocks access | ✅ configurable | ✅ configurable | ✅ Match |
| Infected always blocked | ✅ | ✅ | ✅ Match |
| ClamAV TCP | ✅ via `nClam` | ✅ direct TCP impl | ✅ Equivalent |
| Infected file purge | ✅ | ✅ | ✅ Match |
| Null/Mock providers | ✅ | ✅ | ✅ Match |
| Audit scan events | ✅ | ✅ | ✅ Match |
| RequireCleanScan config | ✅ | ✅ | ✅ Match |
| Access denied audit | ✅ | ⚠️ Partial | ⚠️ Gap |

### 5.2 Known Gaps vs Node.js Service

| Gap | Risk | Priority |
|-----|------|----------|
| `ScanAccessDenied` audit not emitted in .NET | Low (other events cover lifecycle) | Medium |
| No retry on scan failure | Medium | Medium (re-upload is workaround) |
| No scan job persistence across restarts | Medium | Low for v1 |
| No webhook/event on scan complete | Low (clients poll) | Low |
| Queue capacity exposed via config but not wired | Low | Low |

---

## 6. Production Gaps and Recommendations

| Item | Status | Recommendation |
|------|--------|---------------|
| ClamAV in production | Must provision | Run as sidecar (Kubernetes) or use ClamAV SaaS |
| Signature freshclam updates | Must configure | Run `freshclam` on a schedule; or use official ClamAV container |
| Queue durability | Ephemeral | Replace `InMemoryScanJobQueue` with Redis Streams or SQS |
| Scan retry | None | Add exponential backoff with `AttemptCount` field |
| Scan timeout per file type | None | Consider per-MIME-type timeout overrides |
| Alerting on scan failures | None | Wire `ScanStatus.Failed` to a metrics/alerting pipeline |
| DB migration for new columns | Manual | Run `ALTER TABLE` or EF migration before deploying |
| `RequireCleanScanForAccess` default | `true` | HIPAA-aligned default; verify before production |
