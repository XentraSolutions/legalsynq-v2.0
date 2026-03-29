# Final Summary — ClamAV Internal Scanning Implementation

## Service: Documents.NET (port 5006)
## Date: 2026-03-29

---

## Executive Summary

Production-grade, asynchronous ClamAV malware scanning has been implemented as an internal capability of the .NET Documents Service. The implementation follows clean architecture, preserves all existing endpoints, introduces a quarantine-safe upload model, and fails closed on scan errors.

**Build status: ✅ 0 errors, 0 new warnings**

---

## What Was Built

### New Files (7)

| File | Layer | Purpose |
|------|-------|---------|
| `Documents.Domain/Entities/ScanJob.cs` | Domain | In-process scan work item |
| `Documents.Domain/Interfaces/IScanJobQueue.cs` | Domain | Queue port interface |
| `Documents.Infrastructure/Scanner/ClamAvFileScannerProvider.cs` | Infrastructure | TCP-based ClamAV client (INSTREAM protocol) |
| `Documents.Infrastructure/Scanner/InMemoryScanJobQueue.cs` | Infrastructure | Channel-based bounded queue |
| `Documents.Application/Services/ScanOrchestrationService.cs` | Application | Upload→queue coordinator |
| `Documents.Api/Background/DocumentScanWorker.cs` | API | BackgroundService consumer |
| `analysis/dotnet_clamav_phase1_design.md` through `final_summary.md` | Docs | 6 analysis documents |

### Modified Files (9)

| File | Change |
|------|--------|
| `Documents.Domain/Entities/Document.cs` | Added `ScanDurationMs`, `ScanEngineVersion` |
| `Documents.Domain/Enums/AuditEvent.cs` | Added `ScanStarted`, `ScanClean` constants |
| `Documents.Domain/Interfaces/IStorageProvider.cs` | Added `DownloadAsync()` method |
| `Documents.Infrastructure/Storage/LocalStorageProvider.cs` | Implemented `DownloadAsync()` |
| `Documents.Infrastructure/Storage/S3StorageProvider.cs` | Implemented `DownloadAsync()` |
| `Documents.Infrastructure/Database/DocsDbContext.cs` | Mapped new Document scan columns |
| `Documents.Infrastructure/Database/DocumentRepository.cs` | Extended `UpdateScanStatusAsync` with new columns |
| `Documents.Infrastructure/Database/schema.sql` | Added new columns to `documents` table |
| `Documents.Infrastructure/DependencyInjection.cs` | Registered ClamAV, queue, orchestration service |
| `Documents.Application/Services/DocumentService.cs` | Quarantine upload model; async scan dispatch |
| `Documents.Api/Program.cs` | Registered `DocumentScanWorker` as hosted service |
| `Documents.Api/appsettings.json` | Added ClamAV config; set `RequireCleanScanForAccess=true` |

---

## Architecture at a Glance

```
HTTP API Layer
┌─────────────────────────────────────────────┐
│  POST /documents → DocumentService           │
│    ↓ store in quarantine/                    │
│    ↓ ScanStatus = PENDING                    │
│    ↓ enqueue ScanJob                         │
│    → 201 { scanStatus: "PENDING" }           │
└─────────────────────────────────────────────┘
           ↓ IScanJobQueue (Channel)
┌─────────────────────────────────────────────┐
│  DocumentScanWorker (BackgroundService)      │
│    ↓ DownloadAsync(quarantine key)           │
│    ↓ ClamAvFileScannerProvider (TCP clamd)   │
│    ↓ UpdateScanStatusAsync (CLEAN/INFECTED)  │
│    ↓ Infected → DeleteAsync (purge)          │
│    ↓ Audit all scan events                   │
└─────────────────────────────────────────────┘
           ↓
┌─────────────────────────────────────────────┐
│  GET /documents/:id/url                      │
│    → ScanService.EnforceCleanScan()          │
│    → INFECTED/PENDING/FAILED → 403           │
│    → CLEAN → 302 signed URL                  │
└─────────────────────────────────────────────┘
```

---

## Security Posture

| Rule | Implementation |
|------|---------------|
| Infected files never accessible | `EnforceCleanScan()` — unconditional 403 |
| Pending files blocked (default) | `RequireCleanScanForAccess=true` |
| Scan failures blocked (default) | Same config as Pending |
| Infected files purged | `DeleteAsync(storageKey)` in worker |
| Quarantine prefix prevents accidental access | Application-layer key isolation |
| Scan errors → Failed, not Clean | All TCP/storage exceptions map to `Failed` |
| Audit trail for all scan events | 7 audit event types in `document_audits` |
| No sensitive file content logged | Only metadata logged (fileName, size, threats list) |

---

## Scanner Provider Selection

```
Scanner:Provider = "none"    → NullScannerProvider  (Skipped — dev default)
                  "mock"    → MockScannerProvider   (testing)
                  "clamav"  → ClamAvFileScannerProvider (production)
```

---

## Database Schema Changes Required

```sql
-- Add to existing documents table (new columns only)
ALTER TABLE documents
  ADD COLUMN IF NOT EXISTS scan_duration_ms    INT,
  ADD COLUMN IF NOT EXISTS scan_engine_version VARCHAR(100);
```

The `document_versions` table already had these columns.

---

## Outstanding Items (Recommended Follow-ups)

| Item | Effort | Priority |
|------|--------|----------|
| Emit `ScanAccessDenied` audit on blocked access | Small | Medium |
| Wire `ScanWorker:QueueCapacity` config to `InMemoryScanJobQueue` | Small | Low |
| Retry failed scan jobs (exponential backoff) | Medium | Medium |
| Replace in-memory queue with Redis Streams | Medium | High (production) |
| Add `ScanStatus` filter to document list endpoint | Small | Low |
| Webhook/event on scan completion | Large | Low |
| Per-tenant scan policy override | Large | Low |

---

## Parity Grade vs Node.js Service

**Overall: A-** (same grade as pre-ClamAV parity review, no regression)

- All 13 endpoints preserved ✅
- All security controls implemented ✅  
- Async scan model matches Node.js pattern ✅
- Quarantine prefix matches Node.js quarantine logic ✅
- Audit event coverage: 6/7 (access denied gap) ⚠️
- ClamAV TCP implementation equivalent ✅
- Fail-closed defaults ✅
