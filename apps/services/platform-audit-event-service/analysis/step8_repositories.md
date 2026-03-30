# Step 8 — Repository Layer Analysis

**Service**: Platform Audit/Event Service  
**Date**: 2026-03-30  
**Status**: COMPLETE

---

## Overview

The repository layer was largely complete from Step 7 (4 interfaces + 4 EF implementations, registered in Program.cs). This step audited the full implementation, identified two architectural gaps, and delivered targeted additions:

1. **`StreamForExportAsync`** on `IAuditEventRecordRepository` — memory-safe streaming for the export worker using `IAsyncEnumerable<AuditEventRecord>`.
2. **`ListByStatusAsync`** on `IAuditExportJobRepository` — paginated status-filtered job listing for admin dashboards and worker monitoring.
3. **Refactored filter pipeline** in `EfAuditEventRecordRepository` — extracted shared `ApplyFilters` and `ApplySorting` private methods, eliminating duplication between `QueryAsync` and `StreamForExportAsync`.
4. **Corrected visibility filter semantics** — the original `VisibilityScope <= MaxVisibility` filter was semantically inverted; corrected to `VisibilityScope >= MaxVisibility && VisibilityScope != Internal`.

---

## Repositories Created / Confirmed

### 1. `IAuditEventRecordRepository` / `EfAuditEventRecordRepository`

**Persistence target**: `AuditEventRecord`  
**Write semantics**: Append-only — no update or delete methods exposed  
**Table**: `AuditEventRecords`

| Method | Signature | Purpose |
|--------|-----------|---------|
| `AppendAsync` | `(AuditEventRecord, ct) → AuditEventRecord` | Persist a new fully-populated record |
| `GetByAuditIdAsync` | `(Guid, ct) → AuditEventRecord?` | Point lookup by public identifier |
| `ExistsIdempotencyKeyAsync` | `(string?, ct) → bool` | Deduplication probe — short-circuits on null/empty |
| `QueryAsync` | `(AuditRecordQueryRequest, ct) → PagedResult<AuditEventRecord>` | Full filter + sort + paginated query |
| `CountAsync` | `(ct) → long` | Aggregate count for health/diagnostics |
| `GetLatestInChainAsync` | `(string?, string, ct) → AuditEventRecord?` | Latest record in a (TenantId, SourceSystem) chain for `PreviousHash` chaining |
| `StreamForExportAsync` *(added)* | `(AuditRecordQueryRequest, ct) → IAsyncEnumerable<AuditEventRecord>` | Memory-safe streaming for export worker |

**Filter capabilities** (`QueryAsync` / `StreamForExportAsync`):

| Filter | Field | Mode |
|--------|-------|------|
| Scope | TenantId, OrganizationId | Exact match |
| Classification | Category, MinSeverity, MaxSeverity, EventTypes | Enum / IN list |
| Source | SourceSystem, SourceService | Exact match |
| Actor | ActorId, ActorType | Exact / enum |
| Entity | EntityType, EntityId | Exact match |
| Correlation | CorrelationId, SessionId | Exact match |
| Time range | From (≥), To (<) | DateTimeOffset boundaries |
| Visibility | MaxVisibility | ≥ caller level; Internal always excluded |
| Text | DescriptionContains | SQL `CONTAINS` (case-insensitive) |

**Sort options** (`QueryAsync`): `occurredAtUtc` (default), `recordedAtUtc`, `severity`, `sourcesystem`  
**Stream ordering** (`StreamForExportAsync`): ascending `OccurredAtUtc` then `Id` — deterministic and reproducible  
**Page cap**: 500 records per page

---

### 2. `IAuditExportJobRepository` / `EfAuditExportJobRepository`

**Persistence target**: `AuditExportJob`  
**Write semantics**: Create + selective-field Update (lifecycle only)  
**Table**: `AuditExportJobs`

| Method | Signature | Purpose |
|--------|-----------|---------|
| `CreateAsync` | `(AuditExportJob, ct) → AuditExportJob` | Persist a new export job |
| `GetByExportIdAsync` | `(Guid, ct) → AuditExportJob?` | Point lookup by public identifier |
| `UpdateAsync` | `(AuditExportJob, ct) → AuditExportJob` | Update lifecycle fields: Status, FilePath, ErrorMessage, CompletedAtUtc |
| `ListByRequesterAsync` | `(string, int, int, ct) → PagedResult<AuditExportJob>` | Jobs by requester actor, newest first |
| `ListActiveAsync` | `(ct) → IReadOnlyList<AuditExportJob>` | Pending + Processing jobs ordered oldest-first (export worker pickup) |
| `ListByStatusAsync` *(added)* | `(IReadOnlyList<ExportStatus>, int, int, ct) → PagedResult<AuditExportJob>` | Admin/monitoring — filter by one or more statuses; empty list = all statuses |

**UpdateAsync implementation note**: Uses `Attach` + selective `IsModified` marking — avoids a redundant `SELECT` before updating lifecycle fields, and prevents accidental overwrite of immutable fields (`ExportId`, `RequestedBy`, `ScopeType`, `Format`, `CreatedAtUtc`, etc.).

---

### 3. `IIntegrityCheckpointRepository` / `EfIntegrityCheckpointRepository`

**Persistence target**: `IntegrityCheckpoint`  
**Write semantics**: Append-only — re-runs create new records; no correction in place  
**Table**: `IntegrityCheckpoints`

| Method | Signature | Purpose |
|--------|-----------|---------|
| `AppendAsync` | `(IntegrityCheckpoint, ct) → IntegrityCheckpoint` | Persist a new checkpoint |
| `GetByIdAsync` | `(long, ct) → IntegrityCheckpoint?` | Point lookup by surrogate key |
| `GetLatestAsync` | `(string, ct) → IntegrityCheckpoint?` | Most recent checkpoint of a given type (find baseline for next run) |
| `GetByWindowAsync` | `(DateTimeOffset, DateTimeOffset, ct) → IReadOnlyList<IntegrityCheckpoint>` | All checkpoints with FromRecordedAtUtc within a time window |
| `ListByTypeAsync` | `(string, int, int, ct) → PagedResult<IntegrityCheckpoint>` | Paginated checkpoint history by cadence type |

**Design note**: `CheckpointType` is an open string (`"hourly"`, `"daily"`, `"manual"`, etc.) rather than an enum, avoiding schema migrations when new cadences are introduced.

---

### 4. `IIngestSourceRegistrationRepository` / `EfIngestSourceRegistrationRepository`

**Persistence target**: `IngestSourceRegistration`  
**Write semantics**: Upsert by natural key `(SourceSystem, SourceService)`; `SetActiveAsync` updates `IsActive` only  
**Table**: `IngestSourceRegistrations`

| Method | Signature | Purpose |
|--------|-----------|---------|
| `UpsertAsync` | `(IngestSourceRegistration, ct) → IngestSourceRegistration` | Insert new or update `IsActive`/`Notes` on existing |
| `GetBySourceAsync` | `(string, string?, ct) → IngestSourceRegistration?` | Lookup by natural key; `null` service = system-level registration |
| `ListActiveAsync` | `(ct) → IReadOnlyList<IngestSourceRegistration>` | All active registrations for in-memory fast lookup by ingest pipeline |
| `ListAllAsync` | `(int, int, ct) → PagedResult<IngestSourceRegistration>` | Admin paginated list, active + inactive |
| `SetActiveAsync` | `(string, string?, bool, ct) → IngestSourceRegistration?` | Toggle `IsActive` flag; returns null if not found |

---

## DI Registration

All repositories are registered in `Program.cs` as `Scoped`, wired to their EF implementations:

```csharp
builder.Services.AddScoped<IAuditEventRecordRepository,        EfAuditEventRecordRepository>();
builder.Services.AddScoped<IAuditExportJobRepository,          EfAuditExportJobRepository>();
builder.Services.AddScoped<IIntegrityCheckpointRepository,     EfIntegrityCheckpointRepository>();
builder.Services.AddScoped<IIngestSourceRegistrationRepository, EfIngestSourceRegistrationRepository>();
```

All implementations take `IDbContextFactory<AuditEventDbContext>` (also registered as `Scoped`) and `ILogger<T>`. The factory pattern allows each method to open a short-lived `DbContext` for the duration of a single operation, avoiding long-lived context retention and stale change tracker state.

---

## Design Summary

### Append-Only Write Pattern

`AuditEventRecord` and `IntegrityCheckpoint` expose no `UpdateAsync` or `DeleteAsync`. All fields are declared `init`-only on the entity. The repository reinforces this at the contract level — there is no method signature through which a record could be mutated after creation.

### Idempotency Key Lookup

`ExistsIdempotencyKeyAsync` performs a single `AnyAsync` query — no record loading, no materialisation. The ingest pipeline calls this before `AppendAsync` as a fast dedup probe. The unique index on `IdempotencyKey` is the authoritative constraint; `ExistsIdempotencyKeyAsync` is an optimistic pre-check to return a friendly 409 response rather than catching a constraint exception from `SaveChangesAsync`.

### Hash Chain Lookup

`GetLatestInChainAsync(tenantId, sourceSystem)` retrieves the most recently appended record in a `(TenantId, SourceSystem)` chain by ordering on the auto-increment surrogate `Id`. The ingest service reads `Hash` from this record to populate `PreviousHash` on the new record before calling `AppendAsync`.

### Streaming Export

`StreamForExportAsync` returns `IAsyncEnumerable<AuditEventRecord>` via EF Core's `AsAsyncEnumerable()`. The `DbContext` is held open for the full enumeration (within the `async IAsyncEnumerable` iterator state machine) and is disposed when the `await foreach` loop completes or `CancellationToken` fires. The export worker:
1. Translates `ExportRequest` → `AuditRecordQueryRequest`
2. Calls `StreamForExportAsync` with the filter spec
3. `await foreach` to write each record to the output stream
4. Never loads the full result set into memory regardless of dataset size

### Filtered Query Operations

Both `QueryAsync` and `StreamForExportAsync` share the same `ApplyFilters` private method, guaranteeing identical predicate semantics. `QueryAsync` additionally pipes through `ApplySorting`, a `CountAsync` for the total count, and `.Skip().Take()` for pagination.

### Visibility Filter Correction

The original implementation filtered `r.VisibilityScope <= q.MaxVisibility.Value`. Given the enum ordering where `Platform=1` (super-admin only) < `Tenant=2` < `Organization=3` < `User=4` < `Internal=5` (never queryable), this would have returned `Platform`-scoped records for callers with `MaxVisibility=Tenant` — a security defect.

The corrected filter: `r.VisibilityScope >= q.MaxVisibility.Value && r.VisibilityScope != Internal`

This matches the documented intent: "Restrict results to records with VisibilityScope at or below this level" where "below" means *less restricted / more permissive*.

### Portability

- No raw SQL anywhere — all queries expressed as EF Core LINQ translated to MySQL.
- `DbContextFactory` pattern makes all repository methods stateless and safe for concurrent use.
- `AsNoTracking()` applied to all reads — no change-tracker overhead on query paths.
- Enum comparisons in `Where` clauses are translated by EF Core to integer comparisons; no casting needed in application code.

---

## Files Changed

| File | Action |
|------|--------|
| `Repositories/IAuditEventRecordRepository.cs` | Added `StreamForExportAsync` method |
| `Repositories/EfAuditEventRecordRepository.cs` | Added `StreamForExportAsync`; extracted `ApplyFilters`/`ApplySorting`; fixed visibility filter |
| `Repositories/IAuditExportJobRepository.cs` | Added `ListByStatusAsync` method; added `using Enums` |
| `Repositories/EfAuditExportJobRepository.cs` | Added `ListByStatusAsync` implementation |
| `Repositories/IAuditExportJobRepository.cs` *(interface)* | Added `ExportStatus` using import |

All other repository files (`EfIntegrityCheckpointRepository.cs`, `EfIngestSourceRegistrationRepository.cs`, `IAuditEventRepository.cs`, `InMemoryAuditEventRepository.cs`) were unchanged.

---

## Build Verification

```
Build succeeded.
    0 Warning(s)
    0 Error(s)
```

---

## Next Step

**Step 9**: Controller wiring — implement API endpoints for:
- Ingest: `POST /api/auditevents` (single) and `POST /api/auditevents/batch`
- Query: `GET /api/auditevents` (filtered + paginated)
- Record detail: `GET /api/auditevents/{auditId}`
- Export: `POST /api/exports`, `GET /api/exports/{exportId}`
- Source registrations: `GET/POST /api/sources`, `PATCH /api/sources/{system}/{service}/active`
- Integrity: `GET /api/integrity/checkpoints`, `POST /api/integrity/run`
