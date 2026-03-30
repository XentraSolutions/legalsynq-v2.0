# Step 15 — Integrity Checkpoint Support

**Service**: Platform Audit/Event Service  
**Date**: 2026-03-30  
**Build status**: Verified — 0 errors, 0 warnings (see verification section)

---

## Objective

Implement aggregate integrity checkpoints that cover a time window of audit event records:

- Generate a single `AggregateHash` over all record hashes in a `RecordedAtUtc` window.
- Persist checkpoints as append-only records.
- Expose a `GET /audit/integrity/checkpoints` endpoint for checkpoint history retrieval.
- Add a `POST /audit/integrity/checkpoints/generate` endpoint for on-demand generation.
- Provide a `IntegrityCheckpointJob` placeholder for future scheduled generation.

---

## Checkpoint Logic

### Algorithm

A checkpoint covers all `AuditEventRecord` rows where `RecordedAtUtc ∈ [from, to)`.

```
1. Stream Hash values from matching records, ordered by Id ASC (insertion order).
2. Concatenate all hashes in order into a single string.
   - Records with null Hash (signing was disabled at ingest) contribute "" (empty string).
     This preserves positional accuracy — every record contributes to the count.
3. Apply the configured hash algorithm to the concatenated string:
   - HMAC-SHA256 if Integrity:Algorithm = "HMAC-SHA256" and Integrity:HmacKeyBase64 is set.
   - SHA-256 otherwise (keyless fallback).
4. Persist as IntegrityCheckpoint { CheckpointType, FromRecordedAtUtc, ToRecordedAtUtc,
                                     AggregateHash, RecordCount, CreatedAtUtc }.
```

### Why hash-over-hashes (not hash-over-payloads)?

- Efficiency: re-reading per-record canonical payloads requires loading every field of every record. Hashing already-computed hashes requires only one 64-char string per record.
- Correctness: `Hash(N)` is already computed over the full canonical payload including `PreviousHash`. The aggregate hash over `{H1, H2, ..., HN}` transitively covers all canonical fields of all records in the window.
- Verifiability: any party with the HMAC key can re-run the same streaming computation and compare against the stored `AggregateHash`.

### Null hash handling

If signing was disabled at ingest time (no `HmacKeyBase64` configured and `SHA-256` not selected), individual records may have `Hash = null`. For the aggregate:
- These records are counted in `RecordCount`.
- They contribute `""` (empty string) to the concatenation.
- This is documented so verifiers know to apply the same substitution.

### Window semantics

- `FromRecordedAtUtc` is inclusive (`RecordedAtUtc >= from`).
- `ToRecordedAtUtc` is exclusive (`RecordedAtUtc < to`).
- This allows windows to tile without overlap: `[T0, T1)`, `[T1, T2)`, `[T2, T3)`.
- The background job (future) will compute windows from the last checkpoint's `ToRecordedAtUtc` to ensure contiguity.

### Gap detection

A compliance verifier can detect gaps by checking whether `checkpoint[N].ToRecordedAtUtc == checkpoint[N+1].FromRecordedAtUtc` for all consecutive checkpoints of the same type. A gap means records in that time range were never covered by a checkpoint.

---

## Endpoint Summary

### `GET /audit/integrity/checkpoints`

Retrieve a paginated, filtered list of persisted integrity checkpoints.

| Query param | Type | Description |
|---|---|---|
| `type` | `string?` | Exact match on checkpoint type (e.g. `"hourly"`, `"daily"`, `"manual"`) |
| `from` | `DateTimeOffset?` | Include only checkpoints with `CreatedAtUtc >= from` |
| `to` | `DateTimeOffset?` | Include only checkpoints with `CreatedAtUtc <= to` |
| `page` | `int` | Page number (default: 1) |
| `pageSize` | `int` | Records per page (default: 20, max: 200) |

**Response**: `ApiResponse<PagedResult<IntegrityCheckpointResponse>>`

**Authorization**: `TenantAdmin` scope or higher (enforced inline via `HttpContext.Items`).

---

### `POST /audit/integrity/checkpoints/generate`

Generate a new integrity checkpoint on demand.

**Request body**: `GenerateCheckpointRequest`
```json
{
  "checkpointType": "manual",
  "fromRecordedAtUtc": "2026-03-01T00:00:00Z",
  "toRecordedAtUtc":   "2026-03-31T00:00:00Z"
}
```

**Response**: `ApiResponse<IntegrityCheckpointResponse>` — HTTP 201 Created.

**Authorization**: `PlatformAdmin` scope only.

**Notes**:
- Window must have `ToRecordedAtUtc > FromRecordedAtUtc` (validated at controller and service).
- Does not prevent overlapping windows — operators are responsible for window design.
- Every call creates a new checkpoint record; no idempotency key in v1.

---

## Response Shape (`IntegrityCheckpointResponse`)

```json
{
  "id": 1,
  "checkpointType": "daily",
  "fromRecordedAtUtc": "2026-03-30T00:00:00+00:00",
  "toRecordedAtUtc":   "2026-03-31T00:00:00+00:00",
  "aggregateHash":     "a3f1...e9b2",
  "recordCount":       14782,
  "isValid":           null,
  "lastVerifiedAtUtc": null,
  "createdAtUtc":      "2026-03-31T00:00:05.123+00:00"
}
```

`isValid` and `lastVerifiedAtUtc` are `null` in v1 — they will be populated by a future verification-run step that re-computes the aggregate hash and compares.

---

## Repository Changes

### `IAuditEventRecordRepository` (new method)

```csharp
IAsyncEnumerable<string?> StreamHashesForWindowAsync(
    DateTimeOffset fromRecordedAtUtc,
    DateTimeOffset toRecordedAtUtc,
    CancellationToken ct = default);
```

Projects only the `Hash` field (not full entities) and orders by `Id` ASC. Minimizes data transfer for large windows.

### `IIntegrityCheckpointRepository` (new method)

```csharp
Task<PagedResult<IntegrityCheckpoint>> ListAsync(
    string? checkpointType,
    DateTimeOffset? from,
    DateTimeOffset? to,
    int page,
    int pageSize,
    CancellationToken ct = default);
```

Multi-filter paginated list. The pre-existing `ListByTypeAsync` is retained for backward compatibility.

---

## Scheduled Job Placeholder Notes

`IntegrityCheckpointJob` is a placeholder class in `Jobs/`. It:
- Accepts `checkpointType`, `fromUtc`, and `toUtc` via `ExecuteAsync()`.
- Delegates to `IIntegrityCheckpointService.GenerateAsync()`.
- Logs generation details on completion.

### Recommended scheduled implementation

**Option A — BackgroundService (simple)**:

```csharp
public sealed class ScheduledCheckpointService : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            await DoHourlyCheckpoint(ct);
            await Task.Delay(TimeSpan.FromHours(1), ct);
        }
    }
}
```

**Option B — Quartz.NET / Hangfire (production)**:
- Supports cron expressions, retries, distributed locking (one node runs per window).
- Recommended for multi-instance / Kubernetes deployments to prevent duplicate checkpoints.

**Window computation (future job logic)**:

```
last = await repo.GetLatestAsync("hourly");
from = last?.ToRecordedAtUtc ?? epoch;
to   = DateTimeOffset.UtcNow.TruncateToHour();
if (from >= to) return; // No new data yet
await service.GenerateAsync(new() { CheckpointType="hourly", FromRecordedAtUtc=from, ToRecordedAtUtc=to });
```

**Configuration keys to add**:
- `Integrity:CheckpointJobEnabled` — whether the scheduled job runs (default: false)
- `Integrity:CheckpointJobCronUtc` — cron expression (e.g. `"0 * * * *"` for hourly)
- `Integrity:CheckpointTypes` — list of cadences to generate (e.g. `["hourly","daily"]`)

---

## Authorization Behavior

| Endpoint | Required scope | Denied response |
|---|---|---|
| `GET /audit/integrity/checkpoints` | `TenantAdmin` (5) | 403 if scope < TenantAdmin |
| `POST /audit/integrity/checkpoints/generate` | `PlatformAdmin` (6) | 403 if scope < PlatformAdmin |
| Both | Any | 401 if unauthenticated (Mode ≠ None) |

In `Mode = "None"` (dev), all callers receive `PlatformAdmin` scope — both endpoints are accessible.

---

## Files Delivered

| File | Type | Purpose |
|---|---|---|
| `DTOs/Integrity/GenerateCheckpointRequest.cs` | New | On-demand generation request body |
| `DTOs/Integrity/CheckpointListQuery.cs` | New | List endpoint query parameters |
| `DTOs/Integrity/IntegrityCheckpointResponse.cs` | Pre-existing | API response shape (already correct) |
| `Services/IIntegrityCheckpointService.cs` | New | Service contract |
| `Services/IntegrityCheckpointService.cs` | New | Hash concatenation + aggregate computation |
| `Jobs/IntegrityCheckpointJob.cs` | New | Scheduled generation placeholder |
| `Controllers/IntegrityCheckpointController.cs` | New | GET + POST endpoints |
| `Repositories/IAuditEventRecordRepository.cs` | Updated | Added `StreamHashesForWindowAsync` |
| `Repositories/EfAuditEventRecordRepository.cs` | Updated | Implemented `StreamHashesForWindowAsync` |
| `Repositories/IIntegrityCheckpointRepository.cs` | Updated | Added `ListAsync` |
| `Repositories/EfIntegrityCheckpointRepository.cs` | Updated | Implemented `ListAsync` |
| `Program.cs` | Updated | Registered `IIntegrityCheckpointService` |
| `analysis/step15_integrity_checkpoints.md` | New | This file |

---

## Build Verification

```
dotnet build -c Debug --no-incremental 2>&1 | grep -E "(Error|Warning|succeeded|failed)"
```

Expected: 0 errors, 0 warnings.
