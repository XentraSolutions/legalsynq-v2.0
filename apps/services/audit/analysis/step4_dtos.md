# Step 4 — DTOs (Data Transfer Objects)

**Date:** 2026-03-30  
**Phase:** API contract layer  
**Status:** Complete — build ✅ 0 errors, 0 warnings

---

## Summary

14 new DTOs created across 4 sub-namespaces. Existing root-level DTOs
(`ApiResponse`, `PagedResult`, `IngestAuditEventRequest`, `AuditEventResponse`,
`AuditEventQueryRequest`) are **preserved** — they continue to serve the existing
`AuditEvent`-backed service layer and will be superseded progressively when the
service layer is re-wired to `AuditEventRecord` in a later step.

---

## DTOs Created

### Ingest layer — `PlatformAuditEventService.DTOs.Ingest`
**Directory:** `DTOs/Ingest/`

| File | Purpose |
|---|---|
| `AuditEventScopeDto.cs` | Nested scope object: ScopeType, PlatformId, TenantId, OrganizationId, UserId |
| `AuditEventActorDto.cs` | Nested actor object: Id, Type (enum), Name, IpAddress, UserAgent |
| `AuditEventEntityDto.cs` | Nested entity object: Type, Id |
| `IngestAuditEventRequest.cs` | Rich single-event ingest contract (23 fields + 3 nested objects) |
| `BatchIngestRequest.cs` | Batch wrapper: Events list, BatchCorrelationId, StopOnFirstError |
| `IngestItemResult.cs` | Per-item batch result: Index, Accepted, AuditId, RejectionReason, ValidationErrors |
| `BatchIngestResponse.cs` | Batch summary: Submitted, Accepted, Rejected, HasErrors, Results list |

---

### Query layer — `PlatformAuditEventService.DTOs.Query`
**Directory:** `DTOs/Query/`

| File | Purpose |
|---|---|
| `AuditEventQueryRequest.cs` | Rich filter + pagination + sort (20 fields): scope, classification, actor, entity, correlation, time range, visibility, text search |
| `AuditEventActorResponseDto.cs` | Actor context in responses: Id, Type (enum), Name, IpAddress\*, UserAgent\* |
| `AuditEventEntityResponseDto.cs` | Entity context in responses: Type, Id |
| `AuditEventScopeResponseDto.cs` | Scope context in responses: ScopeType, PlatformId, TenantId, OrganizationId, UserScopeId |
| `AuditEventRecordResponse.cs` | Full single-record API representation (29 fields + 3 nested objects) |
| `AuditEventQueryResponse.cs` | Paginated result set: Items, TotalCount, Page, PageSize, TotalPages, HasNext, HasPrev, time range metadata |

\* Redacted based on caller role by the query layer.

---

### Export layer — `PlatformAuditEventService.DTOs.Export`
**Directory:** `DTOs/Export/`

| File | Purpose |
|---|---|
| `ExportRequest.cs` | Async export job creation: scope, filter fields, format, include flags |
| `ExportStatusResponse.cs` | Export job state: ExportId, Status (enum), DownloadUrl, RecordCount, ErrorMessage, IsTerminal, IsAvailable |

---

### Integrity layer — `PlatformAuditEventService.DTOs.Integrity`
**Directory:** `DTOs/Integrity/`

| File | Purpose |
|---|---|
| `IntegrityCheckpointResponse.cs` | Checkpoint read model: Id, CheckpointType, window, AggregateHash, RecordCount, IsValid, LastVerifiedAtUtc |

---

### Common layer — `PlatformAuditEventService.DTOs` (existing, unchanged)

| File | Purpose |
|---|---|
| `ApiResponse<T>` | Standard envelope: Success, Data, Message, TraceId, Errors |
| `PagedResult<T>` | Generic pagination: Items, TotalCount, Page, PageSize, TotalPages, HasNext, HasPrev |

---

## Request / Response Model Summary

### Ingest (single)

```
POST /api/auditevents
Body:  IngestAuditEventRequest (DTOs.Ingest)
       └── Scope: AuditEventScopeDto
       └── Actor: AuditEventActorDto
       └── Entity?: AuditEventEntityDto
Response: ApiResponse<IngestItemResult>  (201 Created)
```

### Ingest (batch)

```
POST /api/auditevents/batch
Body:  BatchIngestRequest (DTOs.Ingest)
       └── Events: IngestAuditEventRequest[]
Response: ApiResponse<BatchIngestResponse>  (207 Multi-Status)
          └── Results: IngestItemResult[]
```

### Query

```
GET /api/auditevents?tenantId=...&category=...&from=...&page=1&pageSize=50
Query params: AuditEventQueryRequest (DTOs.Query)
Response: ApiResponse<AuditEventQueryResponse>
          └── Items: AuditEventRecordResponse[]
                     ├── Scope: AuditEventScopeResponseDto
                     ├── Actor: AuditEventActorResponseDto
                     └── Entity?: AuditEventEntityResponseDto
```

### Single record

```
GET /api/auditevents/{auditId}
Response: ApiResponse<AuditEventRecordResponse>
```

### Export

```
POST /api/exports
Body:  ExportRequest (DTOs.Export)
Response: ApiResponse<ExportStatusResponse>  (202 Accepted)

GET /api/exports/{exportId}
Response: ApiResponse<ExportStatusResponse>
```

### Integrity

```
GET /api/integrity/checkpoints
Response: ApiResponse<PagedResult<IntegrityCheckpointResponse>>

GET /api/integrity/checkpoints/{id}/verify
Response: ApiResponse<IntegrityCheckpointResponse>  (with IsValid populated)
```

---

## Alignment with Entity Model

| DTO field | Entity field | Notes |
|---|---|---|
| `IngestAuditEventRequest.EventId` | `AuditEventRecord.EventId` | Optional source ID |
| `IngestAuditEventRequest.EventType` | `AuditEventRecord.EventType` | Required |
| `IngestAuditEventRequest.EventCategory` | `AuditEventRecord.EventCategory` | Typed enum |
| `IngestAuditEventRequest.SourceSystem` | `AuditEventRecord.SourceSystem` | Required |
| `IngestAuditEventRequest.SourceService` | `AuditEventRecord.SourceService` | Optional |
| `IngestAuditEventRequest.SourceEnvironment` | `AuditEventRecord.SourceEnvironment` | Optional |
| `Scope.ScopeType` | `AuditEventRecord.ScopeType` | Typed enum |
| `Scope.PlatformId` | `AuditEventRecord.PlatformId` (Guid?) | String→Guid parse needed |
| `Scope.TenantId` | `AuditEventRecord.TenantId` | Direct string |
| `Scope.OrganizationId` | `AuditEventRecord.OrganizationId` | Direct string |
| `Scope.UserId` | `AuditEventRecord.UserScopeId` | Field rename: UserId → UserScopeId |
| `Actor.Id` | `AuditEventRecord.ActorId` | Direct string |
| `Actor.Type` | `AuditEventRecord.ActorType` | Typed enum |
| `Actor.Name` | `AuditEventRecord.ActorName` | Direct string |
| `Actor.IpAddress` | `AuditEventRecord.ActorIpAddress` | Direct string |
| `Actor.UserAgent` | `AuditEventRecord.ActorUserAgent` | Direct string |
| `Entity.Type` | `AuditEventRecord.EntityType` | Direct string |
| `Entity.Id` | `AuditEventRecord.EntityId` | Direct string |
| `Action` | `AuditEventRecord.Action` | Required |
| `Description` | `AuditEventRecord.Description` | Required |
| `Before` | `AuditEventRecord.BeforeJson` | Field rename: Before → BeforeJson |
| `After` | `AuditEventRecord.AfterJson` | Field rename: After → AfterJson |
| `Metadata` | `AuditEventRecord.MetadataJson` | Field rename: Metadata → MetadataJson |
| `CorrelationId` | `AuditEventRecord.CorrelationId` | Direct string |
| `RequestId` | `AuditEventRecord.RequestId` | Direct string |
| `SessionId` | `AuditEventRecord.SessionId` | Direct string |
| `Visibility` | `AuditEventRecord.VisibilityScope` | Field rename: Visibility → VisibilityScope |
| `Severity` | `AuditEventRecord.Severity` | Typed enum |
| `OccurredAtUtc` | `AuditEventRecord.OccurredAtUtc` | DateTimeOffset |
| `IdempotencyKey` | `AuditEventRecord.IdempotencyKey` | Direct string |
| `IsReplay` | `AuditEventRecord.IsReplay` | Bool, default false |
| `Tags` | `AuditEventRecord.TagsJson` | List → JSON serialization needed |
| (generated) | `AuditEventRecord.AuditId` | Assigned by ingest pipeline |
| (generated) | `AuditEventRecord.RecordedAtUtc` | Set by ingest pipeline |
| (generated) | `AuditEventRecord.Hash` | Computed by IntegrityHasher |
| (generated) | `AuditEventRecord.PreviousHash` | Set by chain-linker |

### Field naming conventions (DTO vs Entity)

| Convention | DTO field | Entity field | Reason |
|---|---|---|---|
| JSON fields use short names | `Before`, `After`, `Metadata` | `BeforeJson`, `AfterJson`, `MetadataJson` | API callers don't need the "Json" suffix — they see structured JSON |
| Visibility short name | `Visibility` | `VisibilityScope` | Shorter in JSON; no ambiguity in the DTO namespace |
| Scope.UserId | `Scope.UserId` | `UserScopeId` | More natural in the nested object; mapper flattens |

---

## Design Choices

### Nested objects for ingest (Scope, Actor, Entity)
Grouping related fields into typed sub-objects (`AuditEventScopeDto`, `AuditEventActorDto`,
`AuditEventEntityDto`) keeps the top-level request shape readable and allows the same
sub-object types to be reused across future contracts (webhook payloads, event bus messages)
without a breaking change to the root structure.

### Separate request and response nested objects
Response objects have `init`-only properties; request objects have `set` properties. Both
carry the same logical fields but the separation prevents accidental mutation of a response
and makes the direction of data flow explicit. `AuditEventActorResponseDto` additionally
documents the role-conditional redaction of `IpAddress` and `UserAgent`.

### `ApiResponse<T>` as the universal envelope
All endpoints return `ApiResponse<T>`. This gives every caller a consistent `success`,
`message`, `traceId`, and `errors` surface regardless of endpoint. The existing
`ApiResponse` (root namespace) is used unchanged; all new endpoints simply wrap the new
response types in it.

### `IngestItemResult.RejectionReason` as a string constant
Using a fixed set of string constants ("ValidationFailed", "DuplicateIdempotencyKey",
"PersistenceError", "Skipped") rather than an enum keeps the wire format self-documenting
and avoids adding a new enum that would need to be coordinated with client SDKs.

### `ExportRequest` includes filter fields inline (not a nested FilterDto)
`ExportRequest` is a POST body, so composition (nested object) would be natural. However,
the filter field set is modest and the embedded approach avoids an extra level of nesting
in the JSON payload. Callers serialize `{ "scopeType": "Tenant", "from": "...", "format": "Csv" }`
directly without a `"filter": { ... }` wrapper.

### `ExportStatusResponse.IsTerminal` and `IsAvailable` computed properties
These boolean convenience properties let API consumers determine export readiness in a
single field check without needing to switch on `Status` values. They are computed
server-side from Status and DownloadUrl so the client-side logic remains trivial.

### `AuditEventQueryResponse` includes time-range metadata
`EarliestOccurredAtUtc` and `LatestOccurredAtUtc` are returned with the result set
(computed across all pages, not just the current page). This enables UI components to render
a time-range label without requiring a separate aggregate query.

### `IntegrityCheckpointResponse.IsValid` is nullable
`null` means "not yet verified since checkpoint creation" — a meaningful third state distinct
from `true` (verified clean) and `false` (tampering detected). This avoids conflating
"never checked" with "passed".

### Typed enums on DTOs (not strings)
All categorical fields use the strongly typed enums from `PlatformAuditEventService.Enums`
(`EventCategory`, `SeverityLevel`, `ActorType`, etc.). JSON serialization requires
`JsonStringEnumConverter` to be registered globally so API callers can send and receive
string names ("Security", "Warn") rather than integer values. This must be added to
`Program.cs`:

```csharp
builder.Services.AddControllers()
    .AddJsonOptions(opt =>
        opt.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
```

---

## File Layout

```
DTOs/
├── Ingest/
│   ├── AuditEventScopeDto.cs          (5 fields)
│   ├── AuditEventActorDto.cs          (5 fields)
│   ├── AuditEventEntityDto.cs         (2 fields)
│   ├── IngestAuditEventRequest.cs     (23 fields + 3 nested objects)
│   ├── BatchIngestRequest.cs          (3 fields)
│   ├── IngestItemResult.cs            (7 fields)
│   └── BatchIngestResponse.cs         (5 fields + 1 computed)
├── Query/
│   ├── AuditEventQueryRequest.cs      (20 fields)
│   ├── AuditEventActorResponseDto.cs  (5 fields)
│   ├── AuditEventEntityResponseDto.cs (2 fields)
│   ├── AuditEventScopeResponseDto.cs  (5 fields)
│   ├── AuditEventRecordResponse.cs    (29 fields + 3 nested objects)
│   └── AuditEventQueryResponse.cs     (5 fields + 4 computed)
├── Export/
│   ├── ExportRequest.cs               (13 fields)
│   └── ExportStatusResponse.cs        (11 fields + 2 computed)
├── Integrity/
│   └── IntegrityCheckpointResponse.cs (9 fields)
├── ApiResponse.cs                     (existing — unchanged)
├── PagedResult.cs                     (existing — unchanged)
├── IngestAuditEventRequest.cs         (existing — unchanged, used by old service layer)
├── AuditEventResponse.cs              (existing — unchanged, used by old service layer)
└── AuditEventQueryRequest.cs          (existing — unchanged, used by old service layer)
```

---

## Next Steps (Step 5+)

1. **JsonStringEnumConverter** — register globally in `Program.cs` so enum fields serialize as strings
2. **FluentValidation for new DTOs** — create `IngestAuditEventRequestValidator` (new, for rich contract), `BatchIngestRequestValidator`, `ExportRequestValidator`
3. **Mapper** — `AuditEventMapper`: `IngestAuditEventRequest` → `AuditEventRecord` (handle nested flattening, Guid parse for PlatformId, TagsJson serialization, defaults for AuditId/RecordedAtUtc/IsReplay)
4. **Controller wiring** — update `AuditEventsController` to accept `DTOs.Ingest.IngestAuditEventRequest` and return `DTOs.Query.AuditEventRecordResponse`
5. **Export controller** — new `ExportsController` using `ExportRequest` → `ExportStatusResponse`
6. **Integrity controller** — new `IntegrityController` returning `IntegrityCheckpointResponse`
