# Step 11 — Ingestion API Layer: Report

Platform Audit/Event Service  
Date: 2026-03-30

---

## Overview

This step adds the production ingestion API surface for the Platform Audit/Event Service.
Two new endpoints under `/internal/audit` serve machine-to-machine ingest from trusted internal
source systems. The new controller replaces the legacy `POST /api/auditevents` endpoint for all
new source system integrations.

---

## Endpoints Created

### `POST /internal/audit/events`

| Property | Value |
|----------|-------|
| Route | `/internal/audit/events` |
| Controller | `AuditEventIngestController.IngestSingle` |
| Request body | `IngestAuditEventRequest` |
| Response body | `ApiResponse<IngestItemResult>` (success) · `ApiResponse<object>` (error) |
| Validator | `IValidator<IngestAuditEventRequest>` (FluentValidation, injected) |
| Service | `IAuditEventIngestionService.IngestSingleAsync` |

**Purpose:** Accepts a single audit event, validates it, runs it through the ingestion pipeline
(idempotency check → ID generation → hash chain → persist), and returns the assigned `AuditId`.

**Request fields (key):**

| Field | Required | Notes |
|-------|----------|-------|
| `EventType` | Yes | Dot-notation event code. Max 200 chars. |
| `EventCategory` | Yes | Enum: Security, Access, Business, Administrative, System, Compliance, DataChange, Integration, Performance. |
| `SourceSystem` | Yes | Logical system name. Max 200 chars. |
| `SourceService` | Yes | Sub-component within SourceSystem. Max 200 chars. |
| `Scope` | Yes | Tenancy context (ScopeType, TenantId, OrgId, UserId). |
| `Actor` | Yes | Principal who performed the action (Type, Id, Name). |
| `Action` | No | PascalCase verb. Max 200 chars. |
| `Description` | No | Human-readable summary. Max 2000 chars. |
| `OccurredAtUtc` | Yes | Event timestamp. Validated: ≤5 min future, ≤7 years past. |
| `Severity` | Yes | Enum: Debug, Info, Notice, Warn, Error, Critical, Alert. |
| `Visibility` | Yes | Enum: Platform, Tenant, Organization, User, Internal. |
| `IdempotencyKey` | No | Strongly recommended for retry safety. Max 300 chars. |
| `IsReplay` | No | `true` to mark as a replay of a prior event. |
| `Before` / `After` | No | JSON snapshots (verbatim). Max 1 MB each. |
| `Metadata` | No | Arbitrary JSON context (verbatim). Max 1 MB. |
| `Tags` | No | Up to 20 string labels, max 100 chars each. |
| `CorrelationId` / `RequestId` / `SessionId` | No | Distributed tracing identifiers. |

**Success response body (201 Created):**

```json
{
  "success": true,
  "message": "Event accepted.",
  "traceId": "...",
  "data": {
    "index": 0,
    "eventType": "user.login.succeeded",
    "idempotencyKey": "idmp-abc-123",
    "accepted": true,
    "auditId": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
    "rejectionReason": null,
    "validationErrors": []
  },
  "errors": []
}
```

**Error response body (400 / 409 / 503):**

```json
{
  "success": false,
  "message": "Validation failed.",
  "traceId": "...",
  "data": null,
  "errors": ["EventType is required.", "OccurredAtUtc is required."]
}
```

---

### `POST /internal/audit/events/batch`

| Property | Value |
|----------|-------|
| Route | `/internal/audit/events/batch` |
| Controller | `AuditEventIngestController.IngestBatch` |
| Request body | `BatchIngestRequest` |
| Response body | `ApiResponse<BatchIngestResponse>` (success/partial) · `ApiResponse<object>` (400) |
| Validator | `IValidator<BatchIngestRequest>` (FluentValidation, injected; delegates per-item to `IngestAuditEventRequestValidator` via `RuleForEach`) |
| Service | `IAuditEventIngestionService.IngestBatchAsync` |

**Purpose:** Accepts a batch of 1–500 audit events, validates the batch structurally, then runs
each event through the ingestion pipeline independently (default) or stops on first error
(`StopOnFirstError = true`). Per-item results are always returned.

**Request fields:**

| Field | Required | Notes |
|-------|----------|-------|
| `Events` | Yes | List of `IngestAuditEventRequest`. Min 1, max 500. |
| `BatchCorrelationId` | No | Fallback CorrelationId for items that don't supply their own. |
| `StopOnFirstError` | No | Default `false`. When `true`, halts after first rejected item. |

**Success response body (200 — all accepted):**

```json
{
  "success": true,
  "message": "All 3 event(s) accepted.",
  "traceId": "...",
  "data": {
    "submitted": 3,
    "accepted": 3,
    "rejected": 0,
    "hasErrors": false,
    "batchCorrelationId": "batch-xyz",
    "results": [
      { "index": 0, "eventType": "user.login.succeeded", "accepted": true, "auditId": "..." },
      { "index": 1, "eventType": "document.uploaded",    "accepted": true, "auditId": "..." },
      { "index": 2, "eventType": "role.assigned",         "accepted": true, "auditId": "..." }
    ]
  },
  "errors": []
}
```

**Partial success body (207 Multi-Status):**

```json
{
  "success": true,
  "message": "2 of 3 event(s) accepted; 1 rejected. Inspect Results for per-item detail.",
  "data": {
    "submitted": 3,
    "accepted": 2,
    "rejected": 1,
    "hasErrors": true,
    "results": [
      { "index": 0, "accepted": true,  "auditId": "..." },
      { "index": 1, "accepted": false, "rejectionReason": "DuplicateIdempotencyKey", "validationErrors": [] },
      { "index": 2, "accepted": true,  "auditId": "..." }
    ]
  }
}
```

---

## Status Code Summary

### `POST /internal/audit/events`

| Code | Meaning | Trigger |
|------|---------|---------|
| **201 Created** | Event accepted and persisted. | `IngestItemResult.Accepted = true`. Location header set to `/internal/audit/events/{auditId}`. |
| **400 Bad Request** | Structural validation failed. | FluentValidation returned errors before the service was called. Errors list field-level messages. |
| **409 Conflict** | Duplicate idempotency key. | Service returned `RejectionReason = "DuplicateIdempotencyKey"`. |
| **503 Service Unavailable** | Transient infrastructure failure. | Service returned `RejectionReason = "PersistenceError"`. Caller should retry with backoff. |
| **422 Unprocessable Entity** | Unexpected rejection reason. | Service returned a non-null unknown `RejectionReason` not covered by the above cases. |

### `POST /internal/audit/events/batch`

| Code | Meaning | Trigger |
|------|---------|---------|
| **200 OK** | All events accepted. | `response.Accepted == response.Submitted`. |
| **207 Multi-Status** | Partial success. | Some events accepted, some rejected. Always inspect per-item `Results`. |
| **400 Bad Request** | Batch-level structural validation failed. | Outer `BatchIngestRequestValidator` returned errors (batch size, item field errors, BatchCorrelationId length). Body is `ApiResponse<object>` with `Errors` list using `PropertyName: ErrorMessage` format for easy item identification. |
| **422 Unprocessable Entity** | All events rejected. | `response.Accepted == 0`. Body shape is identical to 200/207 — callers always inspect `Results`. |

---

## Controller Design

### Thin controller principle

The controller does exactly three things per endpoint:

1. **Validate** — delegate to the registered `IValidator<T>` and short-circuit with 400 on failure.
2. **Call service** — delegate to `IAuditEventIngestionService` with no business logic in the controller.
3. **Map result** — translate the service's `IngestItemResult` / `BatchIngestResponse` to an HTTP status code and wrap in `ApiResponse<T>`.

No domain logic, no hash computation, no repository calls — all in the service.

### Dependency injection

```
AuditEventIngestController
  ├── IAuditEventIngestionService   (Scoped — AuditEventIngestionService)
  ├── IValidator<IngestAuditEventRequest> (Scoped — IngestAuditEventRequestValidator)
  ├── IValidator<BatchIngestRequest>       (Scoped — BatchIngestRequestValidator)
  └── ILogger<AuditEventIngestController> (framework-provided)
```

All validators are auto-discovered via `AddValidatorsFromAssemblyContaining<>` in `Program.cs`. No manual DI registration needed.

### Validation strategy

**Single endpoint:**  
`IngestAuditEventRequestValidator` runs first. Any structural error returns 400 with all messages.
The service is not called on validation failure.

**Batch endpoint:**  
`BatchIngestRequestValidator` validates the outer batch shape AND each item via `RuleForEach(x => x.Events).SetValidator(itemValidator)`. Errors include the item index prefix (e.g. `Events[2].EventType: EventType is required.`) for pinpoint identification. The service is not called on validation failure.

Note: The service additionally enforces idempotency and persistence safety at runtime, reporting these as per-item `RejectionReason` values rather than structural validation errors.

### Response envelope

All responses are wrapped in `ApiResponse<T>`:

```json
{
  "success": bool,
  "message": "...",
  "traceId": "...",
  "data": T | null,
  "errors": string[]
}
```

`TraceId` is populated from the active `Activity` (W3C traceparent / OpenTelemetry) so callers can correlate ingestion failures with distributed traces.

---

## Swagger Updates

### `PlatformAuditEventService.csproj`

- Added `<GenerateDocumentationFile>true</GenerateDocumentationFile>`.
- Added `<NoWarn>$(NoWarn);1591</NoWarn>` to suppress missing-comment warnings on non-controller types.

### `Program.cs`

- Swagger description updated with endpoint group index.
- XML doc file wired into `IncludeXmlComments()` — surfaces `<summary>`, `<param>`, `<response>` annotations from `AuditEventIngestController` in the Swagger UI.

**Endpoints visible in Swagger UI:**

| Method | Path | Summary |
|--------|------|---------|
| POST | `/internal/audit/events` | Ingest a single audit event from an internal source system. |
| POST | `/internal/audit/events/batch` | Ingest a batch of audit events in a single request. |
| POST | `/api/auditevents` | Legacy: Ingest a single audit/event record (old model). |
| GET  | `/api/auditevents/{id}` | Legacy: Retrieve by ID. |
| GET  | `/api/auditevents` | Legacy: Query with filters. |
| GET  | `/health` | Service liveness probe. |

---

## Files Changed / Created

| File | Change |
|------|--------|
| `Controllers/AuditEventIngestController.cs` | **New** — IngestSingle + IngestBatch endpoints; full ProducesResponseType coverage; XML doc comments; thin controller pattern. |
| `PlatformAuditEventService.csproj` | `GenerateDocumentationFile=true` + `NoWarn 1591`. |
| `Program.cs` | Swagger description updated; `IncludeXmlComments` wired. |
| `Models/Enums/ExportStatus.cs` | Fixed malformed cref attribute (`../Entities/AuditExportJob.cs` → plain text) — surfaced by enabling XML docs. |
| `Data/Configurations/LegacyAuditEventConfiguration.cs` | Fixed unresolvable `<see cref="AuditEventRecord"/>` — replaced with `<c>AuditEventRecord</c>`. |
| `Services/AuditEventIngestionService.cs` | Added missing `<param name="ct">` doc tag on `IngestOneAsync`. |
| `analysis/step11_ingest_api.md` | **New** — this report. |

---

## Build Status

- PlatformAuditEventService: ✅ 0 errors, 0 warnings (verified post-controller creation + XML doc fixes)
