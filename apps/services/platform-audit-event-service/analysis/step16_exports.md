# Step 16 — Audit Export Capability

## Scope

Implement a complete export pipeline for audit event records, including:
- `POST /audit/exports` — create and process an export job
- `GET /audit/exports/{exportId}` — poll job status
- JSON, CSV, and NDJSON output format support
- Local filesystem write (dev/default) with a pluggable storage abstraction
- Full job lifecycle tracking: Pending → Processing → Completed / Failed
- Authorization: same scope constraints as query endpoints
- Documentation: `Docs/exports.md`, this analysis

---

## Export flow

```
POST /audit/exports
       │
       ├─ 1. FluentValidation (ExportRequestValidator)
       │       • Format in ["Json","Csv","Ndjson"]
       │       • ScopeId required for bounded ScopeTypes
       │       • From < To, span ≤ 1 year
       │
       ├─ 2. QueryCallerContext resolved by QueryAuthMiddleware
       │       • Same resolution path as /audit/events
       │
       ├─ 3. ExportRequest → AuditEventQueryRequest mapping
       │       • ScopeType.Tenant  → queryFilter.TenantId = ScopeId
       │       • ScopeType.Org     → queryFilter.OrganizationId = ScopeId
       │       • Category, MinSeverity, EventTypes, ActorId, EntityType/Id,
       │         From, To, CorrelationId propagated
       │
       ├─ 4. IQueryAuthorizer.Authorize(caller, queryFilter)
       │       • Phase 1: scope access check (cross-tenant deny, unknown deny)
       │       • Phase 2: constraint application (TenantId override, ActorId for self)
       │       • UnauthorizedAccessException → 403/401 in controller
       │
       ├─ 5. AuditExportJob created (Status=Pending) via IAuditExportJobRepository
       │
       ├─ 6. Job transitioned to Processing; UpdateAsync called
       │
       ├─ 7. IAuditEventRecordRepository.StreamForExportAsync(queryFilter)
       │       • IAsyncEnumerable<AuditEventRecord>, no pagination
       │       • Ordered: OccurredAtUtc ASC, Id ASC (deterministic)
       │
       ├─ 8. IExportStorageProvider.WriteAsync(exportId, format, writer)
       │       • Opens output stream (file / bucket / container)
       │       • Invokes AuditExportFormatter.WriteAsync(stream, records, exportId, format, opts)
       │
       ├─ 9. AuditExportFormatter.WriteAsync dispatches by format:
       │       • Json   → envelope { exportId, exportedAtUtc, records: [...] }
       │       • Ndjson → one JSON object per line
       │       • Csv    → header row + one flat row per record
       │       • Returns recordCount (long)
       │
       ├─ 10. Job transitioned to Completed
       │        • FilePath = returned storage reference
       │        • RecordCount = count from formatter
       │        • CompletedAtUtc = now
       │        • UpdateAsync called
       │
       └─ 11. 202 Accepted + ExportStatusResponse returned to caller
              (ExportStatusResponse.DownloadUrl = FilePath for Local provider)
```

### Error path

If step 7–9 throws (storage unavailable, disk full, streaming error):
- Job transitioned to Failed
- `ErrorMessage = ex.Message`, `CompletedAtUtc = now`
- `UpdateAsync` called
- `202 Accepted` still returned with `Status="Failed"` (the job itself was processed; the payload reflects the failure)

---

## File formats

### JSON

Streaming envelope format. All records serialised with camelCase property names.
Null fields omitted (`DefaultIgnoreCondition = WhenWritingNull`).

```json
{
  "exportId":      "3f4e5a6b-7c8d-9e0f-1122-334455667788",
  "exportedAtUtc": "2026-03-30T16:12:34.000Z",
  "format":        "Json",
  "records": [
    {
      "auditId":       "...",
      "eventType":     "user.login.succeeded",
      "eventCategory": "Authentication",
      "sourceSystem":  "identity-service",
      "scopeType":     "Tenant",
      "tenantId":      "tenant-abc",
      "actorId":       "user-123",
      "actorType":     "User",
      "action":        "LoginSucceeded",
      "description":   "User authenticated successfully.",
      "severity":      "Low",
      "visibilityScope": "Tenant",
      "occurredAtUtc": "2026-03-30T12:00:00.000+00:00",
      "recordedAtUtc": "2026-03-30T12:00:00.050+00:00",
      "isReplay":      false
    }
  ]
}
```

**Conditional fields** (omitted when corresponding option is false):

| Field | Controlled by |
|---|---|
| `beforeJson`, `afterJson` | `includeStateSnapshots` |
| `hash`, `previousHash` | `includeHashes` AND `QueryAuth:ExposeIntegrityHash` |
| `tags` | `includeTags` |

### NDJSON

One JSON object per line, no envelope. Fields identical to JSON format records.
Suitable for streaming pipelines (AWS Firehose, Google Pub/Sub, Apache Kafka).

```
{"auditId":"...","eventType":"user.login.succeeded",...,"isReplay":false}
{"auditId":"...","eventType":"document.uploaded",...,"isReplay":false}
```

### CSV

Header row + one flat row per record. RFC 4180 escaping (commas and double-quotes
in values are handled correctly). Nested JSON fields (`beforeJson`, `afterJson`,
`metadataJson`) are included as raw JSON strings in their respective columns.

**Static columns** (always present):
`auditId`, `eventId`, `eventType`, `eventCategory`, `sourceSystem`, `sourceService`,
`sourceEnvironment`, `platformId`, `tenantId`, `organizationId`, `userScopeId`,
`scopeType`, `actorId`, `actorType`, `actorName`, `actorIpAddress`, `actorUserAgent`,
`entityType`, `entityId`, `action`, `description`, `metadataJson`, `correlationId`,
`requestId`, `sessionId`, `visibilityScope`, `severity`, `occurredAtUtc`,
`recordedAtUtc`, `idempotencyKey`, `isReplay`

**Conditional columns** (appended when options are true):
`beforeJson`, `afterJson` (if `includeStateSnapshots`)
`hash`, `previousHash` (if `includeHashes`)
`tags` (if `includeTags`)

---

## Job lifecycle

| Status | Terminal | Description |
|---|---|---|
| `Pending = 1` | No | Created, awaiting processing. |
| `Processing = 2` | No | Worker actively building the file. |
| `Completed = 3` | Yes | File written; `FilePath` / `RecordCount` populated. |
| `Failed = 4` | Yes | Processing failed; `ErrorMessage` populated. |
| `Cancelled = 5` | Yes | (Future) cancelled before completion. |
| `Expired = 6` | Yes | (Future) file purged after retention window. |

### State transitions (v1)

```
POST request arrives
  → Pending  (CreateAsync)
  → Processing (UpdateAsync)
  → Completed | Failed (UpdateAsync)
POST request returns 202
```

All transitions happen within the same HTTP request in v1. The `GET` status endpoint
is immediately useful for future async processing without any API changes.

---

## New files

| File | Purpose |
|---|---|
| `Services/Export/IExportStorageProvider.cs` | Storage abstraction contract |
| `Services/Export/LocalExportStorageProvider.cs` | Local filesystem implementation |
| `Services/Export/AuditExportFormatter.cs` | JSON / NDJSON / CSV writers + `ExportRow` projection |
| `Services/IAuditExportService.cs` | Service contract |
| `Services/AuditExportService.cs` | Orchestrator (map → auth → create → process → update) |
| `Controllers/AuditExportController.cs` | `POST /audit/exports` + `GET /audit/exports/{exportId}` |
| `Docs/exports.md` | Operator reference documentation |
| `analysis/step16_exports.md` | This file |

## Modified files

| File | Change |
|---|---|
| `Models/Entities/AuditExportJob.cs` | Added `RecordCount` (nullable long, mutable) |
| `Data/Configurations/AuditExportJobConfiguration.cs` | Added `RecordCount` EF property mapping (`bigint`) |
| `Repositories/EfAuditExportJobRepository.cs` | Mark `RecordCount` modified in `UpdateAsync` |
| `appsettings.Development.json` | Added `Export` section: Provider=Local, LocalOutputPath=exports |
| `Program.cs` | Registered `IExportStorageProvider` + `IAuditExportService`; added export startup log |

---

## Extension points

### Storage providers

The `IExportStorageProvider` interface is the sole touch-point for storage:

```
IExportStorageProvider
  ├── LocalExportStorageProvider   (v1)
  ├── S3ExportStorageProvider      (future)
  └── AzureBlobExportStorageProvider (future)
```

Swap by changing the DI registration in `Program.cs` based on `Export:Provider`.
The service and controller require no changes.

### Async processing

Replace the synchronous `ProcessJobAsync` loop with a `BackgroundService`:

1. `SubmitAsync` creates the job (Pending) and returns immediately with `Status=Pending`.
2. A `BackgroundService` polls `IAuditExportJobRepository.ListActiveAsync()`.
3. Worker picks up Pending jobs, transitions them through Processing → terminal state.
4. `GET /audit/exports/{exportId}` polls the DB — no API changes needed.

### MaxRecordsPerFile

`ExportOptions.MaxRecordsPerFile` is defined and documented. When implemented,
the formatter should split the output after this many records and create
sequentially-numbered files (e.g. `audit-export_...._part01.json`). The job's
`FilePath` would then reference a manifest or the first part.

### Download endpoint

A `GET /audit/exports/{exportId}/download` endpoint can stream the file directly
to the caller for Local provider, or redirect to a pre-signed URL for S3/Azure.
This is out of scope for v1 (file path is returned in `DownloadUrl`).

---

## Authorization model

Reuses the query auth pipeline exactly:

1. `QueryAuthMiddleware` resolves `IQueryCallerContext` from JWT claims (or Anonymous in dev).
2. Controller reads `QueryCallerContext` from `HttpContext.Items`.
3. `AuditExportService.SubmitAsync` passes caller + mapped query to `IQueryAuthorizer.Authorize`.
4. Authorizer applies Phase 1 (access check) + Phase 2 (constraint override) to the query filter.
5. `StreamForExportAsync` executes with the constrained filter — the same predicates as paginated queries.

This means export access is governed by the exact same rules as `GET /audit/events`.
A TenantAdmin who can query their tenant can export it. A TenantUser sees only
user-visible records in their export. A PlatformAdmin can export any scope.

---

## Build status

- 0 errors, 0 warnings (verified post-implementation)
