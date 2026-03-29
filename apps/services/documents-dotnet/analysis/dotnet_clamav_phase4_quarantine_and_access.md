# Phase 4 — Quarantine and Access Enforcement

## Service: Documents.NET (port 5006)
## Date: 2026-03-29

---

## 1. Quarantine Model

### 1.1 Approach: Quarantine Prefix

All files uploaded to the Documents Service land under a `quarantine/` prefix:

```
quarantine/{tenantId}/{documentTypeId}/{unixTimestampMs}.{ext}
```

Example:
```
quarantine/f47ac10b-58cc-4372-a567-0e02b2c3d479/
         ab2d1234-0000-0000-0000-000000000000/
         1743280000000.pdf
```

This prefix is embedded in the storage key via `BuildQuarantineKey()` in `DocumentService`.

### 1.2 Why Prefix (Not Separate Bucket/Container)

| Approach | Pros | Cons |
|----------|------|------|
| **Quarantine prefix** (chosen) | Simple; no file moves; works on local + S3; security via DB | Quarantine semantics enforced by app, not storage ACL |
| Separate quarantine bucket | Storage-layer ACL enforcement possible | S3 copy/delete on promotion; 2x storage code; complex cleanup |
| Signed-URL expiry | URL rotation after scan | Doesn't apply to server-side access; still needs scan gating |

### 1.3 File Lifecycle in Quarantine

```
Upload
  ↓
File stored at: quarantine/{tenantId}/{docTypeId}/{ts}.ext
DB record:      ScanStatus = PENDING

Background Worker scans:
  ↓ CLEAN
DB record:  ScanStatus = CLEAN
File stays: quarantine/{tenantId}/{docTypeId}/{ts}.ext  ← no move needed
Access:     allowed via signed URL / redirect

  ↓ INFECTED
DB record:  ScanStatus = INFECTED
File:       DELETED from quarantine storage  ← purged immediately
Access:     permanently blocked (403)

  ↓ FAILED
DB record:  ScanStatus = FAILED
File stays: quarantine/{tenantId}/{docTypeId}/{ts}.ext
Access:     blocked if RequireCleanScanForAccess=true (default)
```

### 1.4 No Physical File Promotion

Files never move from the quarantine prefix. This is intentional:
- S3: CopyObject + DeleteObject costs money and adds latency
- Local: File rename/copy across paths adds complexity
- Security posture is equivalent: access gated by application code, not storage path

Future option: If storage-ACL-level quarantine isolation is required (e.g., SOC 2 Type II requirement), add a `PromoteAsync(quarantineKey) → cleanKey` method to `IStorageProvider` and call it from the worker after a `Clean` result.

---

## 2. Schema Changes for Quarantine

### 2.1 documents table

Two new columns added to support full scan audit on document records:

```sql
scan_duration_ms    INT,
scan_engine_version VARCHAR(100),
```

These were already present on `document_versions`. Now symmetrical across both tables.

### 2.2 Migration

If using EF Core migrations:
```bash
dotnet ef migrations add AddDocumentScanEngineFields \
  --project Documents.Infrastructure \
  --startup-project Documents.Api
```

If using `schema.sql` (no migrations):
```sql
ALTER TABLE documents
  ADD COLUMN IF NOT EXISTS scan_duration_ms    INT,
  ADD COLUMN IF NOT EXISTS scan_engine_version VARCHAR(100);
```

---

## 3. Scan-Status-Aware Access Enforcement

### 3.1 Enforcement Point

`ScanService.EnforceCleanScan()` is called from `DocumentService.GetSignedUrlAsync()` and `DocumentService.GetContentRedirectAsync()` — the two access pathways.

```csharp
_scan.EnforceCleanScan(doc, _opts.RequireCleanScanForAccess);
```

### 3.2 Enforcement Rules

| ScanStatus | `RequireCleanScanForAccess=false` | `RequireCleanScanForAccess=true` (default) |
|------------|-----------------------------------|--------------------------------------------|
| `Clean` | ✅ Allowed | ✅ Allowed |
| `Skipped` | ✅ Allowed | ✅ Allowed (explicit opt-in to null scanner) |
| `Pending` | ✅ Allowed | ❌ 403 `SCAN_BLOCKED` |
| `Failed` | ✅ Allowed | ❌ 403 `SCAN_BLOCKED` |
| `Infected` | ❌ 403 `SCAN_BLOCKED` | ❌ 403 `SCAN_BLOCKED` |

**Infected is unconditionally blocked** — `RequireCleanScanForAccess` has no effect.

### 3.3 ScanBlockedException

```csharp
throw new ScanBlockedException(
    $"Access denied: file scan status is {status.ToString().ToUpperInvariant()}.");
```

`ExceptionHandlingMiddleware` maps this to:
```json
{
  "error": "SCAN_BLOCKED",
  "message": "Access denied: file scan status is PENDING.",
  "traceId": "..."
}
HTTP 403
```

### 3.4 Access Audit

`ScanAccessDenied` audit event should be added to `ScanService.EnforceCleanScan()`. Current implementation throws without auditing. Recommended follow-up:

```csharp
// In ScanService or DocumentService (after calling EnforceCleanScan):
await _audit.LogAsync(
    AuditEvent.ScanAccessDenied, ctx, doc.Id,
    outcome: "DENIED",
    detail: new { scanStatus = doc.ScanStatus.ToString() });
```

This is flagged as a known gap; the audit event constant (`SCAN_ACCESS_DENIED`) is registered and ready to use.

---

## 4. Infected File Purge

When the background worker detects `ScanStatus.Infected`:

1. Update DB record to `ScanStatus.Infected` (access immediately denied)
2. Call `IStorageProvider.DeleteAsync(storageKey)` — remove file from quarantine
3. Log at `Warning` level with DocumentId, VersionId, threat names
4. If delete fails: log at `Error` level — DB status still blocks access

The two-step approach (DB update first, then storage delete) ensures that even if storage delete fails, the access enforcement gate is already active.

---

## 5. Access Flow for Pending Documents (Client Guidance)

Clients uploading documents should poll for scan completion:

```
POST /documents → 201 { id, scanStatus: "PENDING" }

Loop:
  GET /documents/{id} → { scanStatus: "PENDING" }
  ...wait 1-5 seconds...
  GET /documents/{id} → { scanStatus: "CLEAN" }
  GET /documents/{id}/url → 302 → signed URL
```

Or implement a webhook / notification pattern (future work).

---

## 6. API Response Changes Due to Async Scanning

| Endpoint | Before (inline scan) | After (async scan) |
|----------|---------------------|-------------------|
| `POST /documents` | `scanStatus: "CLEAN"` or `INFECTED` or `SKIPPED` | `scanStatus: "PENDING"` always |
| `POST /documents/:id/versions` | Same as above | `scanStatus: "PENDING"` always |
| `GET /documents/:id/url` | Immediate redirect or 403 | 403 if pending (configurable) |
| `GET /documents/:id` | Always shows final scan status | Shows `PENDING` until worker completes |

**Semantic change:** Callers MUST NOT assume the file is immediately accessible after a successful upload. They should check `scanStatus` before attempting to access the file.
