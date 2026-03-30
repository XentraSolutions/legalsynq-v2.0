# Phase 2: ClamAV Signature Freshness Monitoring & Large-File Policy

## Overview

Step 11 hardened the Documents service with two orthogonal, production-safety features:

1. **Signature Freshness Monitoring** — continuous observability of ClamAV's virus-database age, surfaced through a dedicated health check.
2. **Large-File Policy** — multi-layer enforcement preventing oversized files from reaching the scan pipeline.

---

## 1. Signature Freshness Monitoring

### Goal
ClamAV is only as effective as its virus database.  An engine running stale signatures can miss newly-discovered malware.  This feature tracks signature age and surfaces degradation before it becomes a compliance risk.

### Design Decisions

| Decision | Rationale |
|---|---|
| **Observability-only** (never blocks scans) | Signature age is an operational concern, not an authorization gate.  Blocking scans because of stale signatures would break uploads while the admin updates ClamAV — an unacceptable UX trade-off. |
| **Singleton + 5-minute in-memory cache** | The `VERSION` TCP command is cheap but unnecessary on every scan.  5 minutes balances freshness visibility with noise reduction. |
| **`Degraded` (not `Unhealthy`) health status** | The service remains functionally operational.  `Degraded` correctly signals a problem to operators/monitoring without triggering automatic circuit-breaker restarts. |
| **`IHealthCheck` (not `IHostedService`)** | Health checks participate in the existing `/health/ready` endpoint.  A background service would require separate alerting wiring. |

### ClamAV VERSION Protocol

`clamd` responds to `VERSION\n` with:

```
ClamAV <engine>/<db-version>/<db-date>
```

Example: `ClamAV 0.103.8/26830/Fri Jul 14 09:20:13 2023`

The `db-date` field uses ctime-style formatting with a space-padded day — parsed with `ParseExact` against the pattern `ddd MMM  d HH:mm:ss yyyy` (two-space gap before single-digit days).

### Component Summary

| Class | Location | Responsibility |
|---|---|---|
| `ClamAvSignatureFreshnessMonitor` | `Infrastructure/Scanner/` | Connects via TCP, sends `VERSION\n`, parses response, caches result 5 min |
| `ClamAvSignatureHealthCheck` | `Infrastructure/Health/` | Calls monitor, returns `Healthy` / `Degraded` based on `SignatureMaxAgeHours` |
| `ClamAvSignatureInfo` | `Infrastructure/Scanner/` (nested in monitor file) | Immutable snapshot: Success, RawVersion, EngineVersion, DbVersion, DbDate, AgeHours |

### Configuration

```json
"Scanner": {
  "ClamAv": {
    "SignatureMaxAgeHours": 24
  }
}
```

Default: **24 hours**.  Adjust based on organizational update cadence (daily freshclam = 24h is correct; every 12h = set to 12).

### Health Check Registration

```csharp
.AddCheck<ClamAvSignatureHealthCheck>("clamav-signatures",
    failureStatus: HealthStatus.Degraded,
    tags: new[] { "ready" })
```

Visible at `GET /health/ready` — aggregated with `database` and `clamav` checks.

---

## 2. Large-File Policy

### Goal
Prevent files above configurable limits from entering the system at all — before they consume bandwidth, storage, or scan-pipeline resources.

### Multi-Layer Architecture

```
HTTP Request (multipart/form-data)
    │
    ▼
┌──────────────────────────────────────┐
│  Endpoint Layer  (Documents.Api)     │  ← Layer 1: Early check (413)
│  file.Length > MaxUploadSizeMb       │    Increments UploadFileTooLargeTotal
│                                      │    Returns before reading full body
└──────────────────────────────────────┘
    │ passes
    ▼
┌──────────────────────────────────────┐
│  DocumentService  (Application)      │  ← Layer 2: Scan-limit guard
│  content.Length > MaxScannableFileSizeMb  Throws FileSizeExceedsScanLimitException
└──────────────────────────────────────┘
    │ exception
    ▼
┌──────────────────────────────────────┐
│  ExceptionHandlingMiddleware (Api)   │  ← Layer 3: Metric + structured response
│  catch FileSizeExceedsScanLimitEx    │    Increments ScanSizeRejectedTotal
│  → HTTP 422                          │    Returns JSON with limitMb, fileSizeBytes
└──────────────────────────────────────┘
```

### Why Two Separate Limits?

| Limit | Config Key | Purpose |
|---|---|---|
| `MaxUploadSizeMb` | `Documents:MaxUploadSizeMb` | Max file the HTTP layer will accept.  First gate — protects bandwidth and storage. |
| `MaxScannableFileSizeMb` | `Documents:MaxScannableFileSizeMb` | Max file ClamAV will attempt to scan.  Must be ≤ upload limit or files can be accepted but never scanned (compliance gap). |

In normal operation both values are identical.  They are kept separate so operators can:
- Temporarily widen upload limit while raising ClamAV's `StreamMaxLength`
- Accept but quarantine large files outside the scan pipeline (future use-case)

### Startup Validation

`DependencyInjection.ValidateFileSizeConfiguration()` runs at startup:

| Condition | Result |
|---|---|
| `MaxUploadSizeMb > MaxScannableFileSizeMb` | **Hard fail** — `InvalidOperationException` thrown; service won't start |
| `Documents:MaxScannableFileSizeMb > Scanner:ClamAv:MaxScannableFileSizeMb` | **Warning** logged — ClamAV may reject scans above its own limit |

### New Exceptions

| Exception | HTTP Status | Metric Incremented |
|---|---|---|
| `FileTooLargeException` | 413 | `documents_scan_upload_too_large_total` |
| `FileSizeExceedsScanLimitException` | 422 | `documents_scan_size_rejected_total` |

### New Metrics (cumulative)

| Metric | Description |
|---|---|
| `documents_scan_upload_too_large_total` | Files rejected at the HTTP upload boundary (413) |
| `documents_scan_size_rejected_total` | Files rejected at the scan-limit boundary (422) |
| `documents_scan_clamav_circuit_open_total` | Circuit breaker trips (from Step 10) |

### Configuration

```json
"Scanner": {
  "ClamAv": {
    "MaxScannableFileSizeMb": 25
  }
},
"Documents": {
  "MaxUploadSizeMb": 25,
  "MaxScannableFileSizeMb": 25
}
```

Default: **25 MB** for both.  Align with ClamAV's `StreamMaxLength` setting in `clamd.conf`.

---

## 3. Extended `ClamAvOptions`

```csharp
public sealed class ClamAvOptions
{
    // … existing fields …
    public int SignatureMaxAgeHours    { get; set; } = 24;
    public int MaxScannableFileSizeMb  { get; set; } = 25;
}
```

## 4. Extended `DocumentServiceOptions`

```csharp
public sealed class DocumentServiceOptions
{
    // … existing fields …
    public int MaxUploadSizeMb         { get; set; } = 25;
    public int MaxScannableFileSizeMb  { get; set; } = 25;
}
```

---

## 5. Testing Guidance

### Signature Freshness

| Scenario | Expected result |
|---|---|
| ClamAV updated within `SignatureMaxAgeHours` | `/health/ready` → `Healthy` for `clamav-signatures` |
| ClamAV database older than `SignatureMaxAgeHours` | `/health/ready` → `Degraded` for `clamav-signatures` |
| ClamAV not reachable | `Success=false`, `Degraded` with socket-error description |
| Cache hot (< 5 min since last call) | Second call returns cached result, no TCP connection made |

### File-Size Policy

| Scenario | Expected HTTP status | Metric incremented |
|---|---|---|
| File exactly at `MaxUploadSizeMb` | 200 (allowed) | — |
| File 1 byte over `MaxUploadSizeMb` | 413 | `UploadFileTooLargeTotal` |
| `MaxUploadSizeMb < MaxScannableFileSizeMb` at startup | service won't start | — |
| `MaxUploadSizeMb == MaxScannableFileSizeMb` (normal) | service starts, no warning | — |

---

## 6. Alignment with Compliance Requirements

| Requirement | Implementation |
|---|---|
| HIPAA: No PHI processed by stale AV engine | `ClamAvSignatureHealthCheck` surfaces degradation; ops SLA ensures freshclam runs daily |
| HIPAA: Audit trail for rejected uploads | `ExceptionHandlingMiddleware` logs file name, size, limit, correlationId at `Warning` level |
| SOC 2: Metrics for capacity and anomaly detection | `UploadFileTooLargeTotal` + `ScanSizeRejectedTotal` exported via Prometheus endpoint |
| Defense-in-depth | Three independent enforcement layers prevent any single bypass |
