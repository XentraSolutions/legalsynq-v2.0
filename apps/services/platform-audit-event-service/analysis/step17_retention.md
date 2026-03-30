# Step 17 — Retention and Archival Foundations

**Service**: PlatformAuditEventService  
**Date**: 2026-03-30  
**Build status**: ✅ 0 errors, 0 warnings  
**Files created**: 11  
**Files updated**: 7  

---

## Objective

Establish the retention and archival foundation for the audit event service:
- Config-driven, tier-aware retention policy evaluation
- Pluggable archival provider abstraction (no actual writes in v1)
- Scheduled job entry point (evaluation only in v1)
- Legal hold compatibility defined as a documented future extension
- Zero impact on existing data — no records modified, archived, or deleted

---

## What Was Implemented

### 1. Storage tier model (`StorageTier` enum)

Five tiers classify where a record sits in its lifecycle:

| Tier | Condition | v1 behavior |
|---|---|---|
| Hot | Age ≤ HotRetentionDays | Full access, no action |
| Warm | Past Hot window, within full retention | Candidate for archival (not yet executed) |
| Cold | Past full retention window | Eligible for archival + deletion (not yet executed) |
| Indefinite | RetentionDays = 0 | Never purge |
| LegalHold | Explicit hold (future) | Exempt from all enforcement |

### 2. Retention policy configuration (`RetentionOptions`)

Expanded from the prior placeholder with:

| New field | Default | Purpose |
|---|---|---|
| `HotRetentionDays` | 365 | Hot/Warm tier boundary |
| `DryRun` | `true` | Evaluation-only gate; prevents any deletion in v1 |
| `LegalHoldEnabled` | `false` | Reserved; triggers hold-check logic when implemented |

Existing fields retained: `DefaultRetentionDays`, `CategoryOverrides`, `TenantOverrides`, `JobEnabled`, `JobCronUtc`, `MaxDeletesPerRun`, `ArchiveBeforeDelete`.

Policy resolution order (highest → lowest priority):
1. Legal hold (future)
2. Per-tenant override (`TenantOverrides[tenantId]`)
3. Per-category override (`CategoryOverrides[category]`)
4. Default (`DefaultRetentionDays`)
5. 0 → Indefinite

### 3. Archival provider abstraction

Three-layer abstraction pattern (mirrors Export provider design):

```
IArchivalProvider              ← interface (Services/Archival/)
  └── NoOpArchivalProvider     ← v1 — counts records, logs, writes nothing
  └── (future) LocalCopyArchivalProvider
  └── (future) S3ArchivalProvider
  └── (future) AzureBlobArchivalProvider
```

Companion types:
- `ArchivalContext` — carries job metadata (jobId, window, tenantId, initiator)
- `ArchivalResult` — structured result (recordsProcessed, archived, destination, success/error)
- `ArchivalOptions` — `Archival:Strategy`, `BatchSize`, provider-specific connection details

`NoOpArchivalProvider` streams all records to get an exact count, logs the would-be outcome, and returns a success result. This validates the pipeline wiring without any writes.

### 4. Retention service (`IRetentionService` / `RetentionService`)

Core evaluation engine. All methods are read-only.

| Method | Description |
|---|---|
| `ResolveRetentionDays(record)` | Returns effective retention days for a record (tenant > category > default) |
| `ComputeExpirationDate(record)` | `RecordedAtUtc + days`, or `null` for indefinite |
| `ClassifyTier(record)` | Returns `StorageTier` based on age and retention window |
| `EvaluateAsync(request, ct)` | Scans a sample of oldest records; returns tier breakdown + policy summary |
| `BuildPolicySummary()` | Human-readable policy description for logs and evaluation results |

`EvaluateAsync` uses `IAuditEventRecordRepository.CountAsync` for the total and `QueryAsync` (oldest-first, capped at `SampleLimit`) for tier classification. No new repository methods were needed.

### 5. Evaluation DTOs

**`RetentionEvaluationRequest`**:
- `TenantId?` — scope to a tenant
- `Category?` — scope to a category
- `SampleLimit` — max records to classify (default 5000)

**`RetentionEvaluationResult`**:
- `TotalRecordsInStore` — live aggregate count
- `SampleRecordsClassified` — records actually classified
- `RecordsInHotTier`, `Warm`, `Cold`, `Indefinite`, `OnLegalHold`
- `RecordsExpiredInSample` — count of Cold-tier records in sample
- `ExpiredByCategory` — breakdown by category string
- `OldestRecordedAtUtc` — oldest record in sample
- `PolicySummary` — human-readable policy description
- `IsDryRun` — always `true` in v1
- `EvaluatedAtUtc`

### 6. Retention policy job (`RetentionPolicyJob`)

Replaces the empty placeholder. Each run:

1. Short-circuits if `Retention:JobEnabled=false`.
2. Calls `IRetentionService.EvaluateAsync()` with `SampleLimit = MaxDeletesPerRun`.
3. Logs structured tier counts at `Information` level.
4. Logs a `Warning` per expired category when Cold-tier records exist.
5. Logs an activation guidance message when `DryRun=false && ArchiveBeforeDelete=true` (not yet implemented).
6. Returns — no writes of any kind.

### 7. DI registration (Program.cs)

```
builder.Services.Configure<ArchivalOptions>(...)   // singleton options
builder.Services.AddScoped<IRetentionService, RetentionService>()
builder.Services.AddSingleton<IArchivalProvider, NoOpArchivalProvider>()
builder.Services.AddTransient<RetentionPolicyJob>()
```

Startup log:
- `Warning` when `Retention:JobEnabled=false` (correct default)
- `Information` with policy summary when job is enabled

### 8. Configuration

`appsettings.json` — full Retention + Archival sections with all defaults documented.  
`appsettings.Development.json` — dev overrides (DryRun=true, Strategy=NoOp, JobEnabled=false).

### 9. Documentation

- `Docs/retention-and-archival.md` — operator reference: tier model, config table, legal hold spec, archival provider extension guide.

---

## Retention Model

```
Record age (days from RecordedAtUtc)
     0 ─────────────────────────────────────────────────────────────►
     │← Hot window →│←── Warm window ──────────────────────────────►│
     0         HotRetentionDays                   DefaultRetentionDays
                                                  (or tenant/category override)
                    ↑                                                ↑
                Warm starts                                     Cold starts
                (archival candidate)                       (deletion eligible)

     If DefaultRetentionDays = 0: entire range is Indefinite (no purge ever)
     If record is on LegalHold (future): overrides all tier classification
```

Policy priority (descending):

```
LegalHold check (future) → TenantOverride → CategoryOverride → DefaultRetentionDays → 0=Indefinite
```

---

## Archival Model

```
Hot store (primary DB)
  ↓ [Cold tier identified by RetentionService.EvaluateAsync()]
  ↓ [Archival enabled: ArchiveBeforeDelete=true, DryRun=false]
  ↓
IArchivalProvider.ArchiveAsync(records, context, ct)
  ├─ NoOpArchivalProvider       → stream count + log (v1)
  ├─ LocalCopyArchivalProvider  → write NDJSON to local dir (planned)
  ├─ S3ArchivalProvider         → upload to S3 (planned)
  └─ AzureBlobArchivalProvider  → upload to Azure Blob (planned)
  ↓ [ArchivalResult.IsSuccess == true]
  ↓
Purge from hot store             ← NOT implemented; requires explicit compliance workflow
```

Archival context passed per job:

```
ArchivalContext {
  ArchiveJobId   string        -- correlation ID
  WindowFrom     DateTimeOffset
  WindowTo       DateTimeOffset
  TenantId       string?
  Category       string?
  InitiatedBy    string
  InitiatedAtUtc DateTimeOffset
}
```

---

## What Is Implemented Now

| Component | Status |
|---|---|
| `StorageTier` enum (Hot/Warm/Cold/Indefinite/LegalHold) | ✅ Complete |
| `ArchivalStrategy` enum | ✅ Complete |
| `RetentionOptions` (expanded) | ✅ Complete |
| `ArchivalOptions` config class | ✅ Complete |
| `IArchivalProvider` interface | ✅ Complete |
| `ArchivalContext` / `ArchivalResult` | ✅ Complete |
| `NoOpArchivalProvider` | ✅ Complete |
| `IRetentionService` interface | ✅ Complete |
| `RetentionService` (evaluation, dry-run) | ✅ Complete |
| `RetentionEvaluationRequest` / `RetentionEvaluationResult` | ✅ Complete |
| `RetentionPolicyJob` (evaluation + structured logging) | ✅ Complete |
| DI registration in Program.cs | ✅ Complete |
| Startup log (job enabled/disabled + policy summary) | ✅ Complete |
| `appsettings.json` (full Retention + Archival sections) | ✅ Complete |
| `appsettings.Development.json` (dev overrides) | ✅ Complete |
| `Docs/retention-and-archival.md` | ✅ Complete |
| README retention section | ✅ Complete |
| Build: 0 errors, 0 warnings | ✅ Verified |

---

## What Remains for Production Hardening

Listed by priority:

### High — required before activating retention in production

| Item | Description |
|---|---|
| **Scheduler wiring** | Register `RetentionPolicyJob` as a `BackgroundService` or Quartz.NET trigger using `Retention:JobCronUtc`. Without a scheduler, `ExecuteAsync` is never called. |
| **`LocalCopyArchivalProvider`** | Implement streaming NDJSON write to a local directory. First real archival backend; validates the `IArchivalProvider` contract end-to-end. |
| **Archival-then-delete pipeline** | When `DryRun=false && ArchiveBeforeDelete=true`: query Cold-tier records in batches, call `IArchivalProvider.ArchiveAsync`, then delete the batch from the primary store. Wrap in a transaction or two-phase commit pattern. |
| **Integrity checkpoint pre-check** | Before archiving a window, verify that an `IntegrityCheckpoint` exists covering that window. Prevents archiving records whose hash chain has not been validated. |
| **Per-record legal hold entity** | `LegalHold` table: `HoldId, AuditEventId, HeldBy, HeldAtUtc, ReleasedAtUtc, LegalAuthority`. Pre-check in `RetentionService.ComputeExpirationDate`. Compliance workflow for hold creation and release. |

### Medium — important for cloud production deployments

| Item | Description |
|---|---|
| `S3ArchivalProvider` | Streams records as NDJSON to S3. Uses multipart upload for batches > 5 MB. |
| `AzureBlobArchivalProvider` | Streams records to Azure Blob Storage. |
| Archive-only mode (no deletion) | For WORM-compatible storage: write to archive, mark records as `Archived` in the primary store, never delete. Useful for immutable audit requirements. |
| Soft-delete before hard-delete | Add an `IsDeleted` / `DeletedAtUtc` tombstone to `AuditEventRecord`. Soft-delete first; hard-delete after a configurable grace period. |
| Archival job tracking entity | A `RetentionJobRun` entity: jobId, startedAt, completedAt, recordsEvaluated, recordsArchived, recordsDeleted, status. Queryable via admin API. |

### Low — operational improvements

| Item | Description |
|---|---|
| `GET /audit/retention/evaluation` | Admin endpoint: trigger an on-demand evaluation and return `RetentionEvaluationResult` as JSON. |
| `POST /audit/retention/jobs` | Admin endpoint: manually trigger a retention run with an optional `dryRun` override. |
| Per-category evaluation endpoint | Scoped evaluation result for a specific category. Useful for compliance officers reviewing a single data class. |
| Alerting integration | Emit a structured log event or metric when Cold-tier records exceed a threshold. Hook into Prometheus, Datadog, or CloudWatch alerts. |
| Archive verification | After writing an archive file, re-read it and verify the record count and a sample of hashes against the primary store. Prevents silent archival corruption. |

---

## Design Decisions

**Why evaluation-only in v1?**  
Audit record deletion is the most consequential operation in this service. Doing it incorrectly cannot be undone. The evaluation-first approach lets operators observe the policy in production, confirm tier distributions, and validate archival destinations before enabling deletion. The `DryRun=true` default makes this explicit in configuration.

**Why `NoOpArchivalProvider` instead of nothing?**  
A live no-op wires the full DI graph and validates that the retention service correctly identifies Cold-tier records, streams them, and counts them — without any storage risk. This is the same pattern used for the `LocalExportStorageProvider` in Step 16.

**Why sample-based evaluation rather than full-table scan?**  
Full-table classification at evaluation time would lock the database under heavy load and produce misleading results for large deployments. The sample (oldest-first) focuses on the records most likely to be expired, which is where operators need visibility. A future production implementation would use aggregate SQL queries (`COUNT(*) WHERE recorded_at_utc < :cutoff`) for efficiency.

**Why not delete from the hot store in v1?**  
Three prerequisites are not yet met: (1) no production archival provider is implemented, (2) integrity checkpoints do not yet gate archival windows, and (3) legal hold is not tracked per-record. Deleting without all three creates compliance risk that cannot be remediated after the fact.
