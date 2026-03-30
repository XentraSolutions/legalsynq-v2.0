# Step 9 — Ingestion Service Layer

**Service**: Platform Audit/Event Service  
**Files created**: `Services/IAuditEventIngestionService.cs`, `Services/AuditEventIngestionService.cs`, `Utilities/AuditRecordHasher.cs`  
**Files modified**: `Mappers/AuditEventRecordMapper.cs`, `Program.cs`  
**Build result**: 0 errors · 0 warnings

---

## 1. Service Interface

`IAuditEventIngestionService` exposes two methods:

| Method | Input | Output |
|--------|-------|--------|
| `IngestSingleAsync` | `IngestAuditEventRequest` | `IngestItemResult` |
| `IngestBatchAsync`  | `BatchIngestRequest`      | `BatchIngestResponse` |

The interface carries no infrastructure concerns. Validation, authorization, and response projection are handled at the layers above (FluentValidation, middleware, controller).

---

## 2. Ingestion Pipeline (per event)

```
Caller → IngestSingleAsync / IngestBatchAsync
               │
               ▼
    ┌─────────────────────┐
    │ 1. Idempotency probe│  ExistsIdempotencyKeyAsync(key)
    │    (if key present) │  → DuplicateIdempotencyKey if true
    └────────┬────────────┘
             │
             ▼
    ┌─────────────────────┐
    │ 2. ID + time gen    │  auditId = Guid.NewGuid()
    │                     │  now = DateTimeOffset.UtcNow
    └────────┬────────────┘
             │
             ▼
    ┌─────────────────────┐  (skipped when HmacKeyBase64 not set)
    │ 3. Chain lookup     │  GetLatestInChainAsync(tenantId, sourceSystem)
    │                     │  → previousHash = chainHead?.Hash
    └────────┬────────────┘
             │
             ▼
    ┌─────────────────────┐  (skipped when HmacKeyBase64 not set)
    │ 4. Hash computation │  AuditRecordHasher.Compute(auditId, fields..., hmacSecret)
    │                     │  → hash = HMAC-SHA256 hex string
    └────────┬────────────┘
             │
             ▼
    ┌─────────────────────┐
    │ 5. Entity mapping   │  AuditEventRecordMapper.ToEntity(req, auditId, now,
    │                     │    correlationIdOverride, hash, previousHash)
    └────────┬────────────┘
             │
             ▼
    ┌─────────────────────┐
    │ 6. AppendAsync      │  IAuditEventRecordRepository.AppendAsync(entity, ct)
    │                     │  → catch DbUpdateException (unique constraint)
    └────────┬────────────┘
             │
             ▼
    ┌─────────────────────┐
    │ 7. IngestItemResult │  Accepted=true, AuditId=persisted.AuditId
    │    returned         │  OR Accepted=false, RejectionReason=...
    └─────────────────────┘
```

---

## 3. Idempotency Behaviour

### Key supplied by caller

1. **Pre-check probe**: `ExistsIdempotencyKeyAsync(key)` runs before any write.
   - Hit → return `IngestItemResult { Accepted=false, RejectionReason="DuplicateIdempotencyKey" }`.
   - Miss → continue pipeline.

2. **Concurrent duplicate guard**: Two concurrent requests with the same key can both pass the probe before either commits. The database's unique index on `IdempotencyKey` enforces the constraint at commit time.
   - `DbUpdateException` from a unique-constraint violation is caught and translated to `DuplicateIdempotencyKey` rather than surfacing as a 500.
   - For MySQL/Pomelo: `MySqlException.ErrorCode == 1062` (ER_DUP_ENTRY) is checked directly.
   - Portable fallback: message substring match for SQLite (used in unit tests).

### No key supplied

- No idempotency check is performed.
- The caller is responsible for ensuring the request is safe to replay.
- This is intentional — mandatory key enforcement would break event-fire-and-forget producers (e.g. automated system events where generating a stable key is impractical).

---

## 4. Replay Behaviour

`IngestAuditEventRequest.IsReplay = true` is a **semantic marker** only. At the service level:

| Aspect | Behaviour |
|--------|-----------|
| Idempotency check | Still enforced if an `IdempotencyKey` is provided |
| AuditId | New server-assigned GUID — not the original event's ID |
| RecordedAtUtc | Server receipt time — not the original ingestion time |
| OccurredAtUtc | Preserved from the request (the original event timestamp) |
| Chain participation | Yes — the replay record gets a `PreviousHash` and contributes a new `Hash` |
| Persistence | Normal append — no skip, no merge with existing records |

The `IsReplay` flag is stored on the record and available in queries. It has no effect on storage, hashing, or validation logic.

**Use cases**:
- Re-ingesting audit data after a migration or schema change.
- Backfilling events from a source system that was not integrated at event time.
- Replaying events from a dead-letter queue after a transient ingestion failure.

---

## 5. Batch Semantics

### `StopOnFirstError = false` (default)

All items in `BatchIngestRequest.Events` are attempted independently. Each produces its own `IngestItemResult`. The response always has `Results.Count == Events.Count`.

### `StopOnFirstError = true`

Processing halts immediately after the first rejected item. All untried items receive `IngestItemResult { Accepted=false, RejectionReason="Skipped" }`. Items are never partially processed (no gap between processed and skipped).

### `BatchCorrelationId` propagation

If an individual item does not supply a `CorrelationId`, the `BatchCorrelationId` from the batch request is used. This ensures all records in the batch are traceable as a unit in the audit log. If the item has its own `CorrelationId`, it is not overwritten.

---

## 6. Integrity Hash Design

### `AuditRecordHasher` (new, targets `AuditEventRecord`)

Canonical field set (order is fixed — reordering breaks existing records):

```
AuditId | EventType | SourceSystem | TenantId | ActorId |
EntityType | EntityId | Action | OccurredAtUtc (O) | RecordedAtUtc (O)
```

- Fields present: `pipe`-delimited, `"O"` round-trip ISO 8601 format for datetimes.
- Fields absent (null): encoded as empty string — prevents reordering attacks.
- Algorithm: HMAC-SHA256 with a 256-bit secret; output is lowercase hex.

### Why hash is computed before entity creation

`AuditEventRecord` uses `init`-only properties. Hashes must be set at object-initialiser time; there is no post-construction setter. The hash covers `AuditId` and `RecordedAtUtc`, which would normally be generated inside the mapper. The solution:

1. Service generates `auditId` and `now` itself (step 2).
2. Service computes the hash using those known values (step 4).
3. Service passes all three (`auditId`, `hash`, `previousHash`) to the mapper (step 5).
4. Mapper constructs the entity in one allocation with all fields filled in.

This avoids mutable fields, double-allocation, or reflection-based property setting.

### When signing is disabled

If `IntegrityOptions.HmacKeyBase64` is null or empty, steps 3 and 4 are skipped entirely. `Hash` and `PreviousHash` are null on the persisted record. The service never throws due to a missing key — it silently omits hashes. This allows development and staging environments to run without configuring a key.

---

## 7. Mapper Signature Change

`AuditEventRecordMapper.ToEntity` gained new optional parameters:

```csharp
public static AuditEventRecord ToEntity(
    IngestAuditEventRequest req,
    Guid            auditId,              // caller-generated (was Guid.NewGuid() inside mapper)
    DateTimeOffset  now,
    string?         correlationIdOverride = null,
    string?         hash                 = null,
    string?         previousHash         = null)
```

- `auditId` is now required (no default) — breaks any direct mapper call that omits it, making the responsibility explicit.
- `correlationIdOverride` only overrides when the item has no `CorrelationId`; the item's own value is never discarded.

---

## 8. Rejection Reasons

| Reason | Cause | Retryable |
|--------|-------|-----------|
| `DuplicateIdempotencyKey` | Pre-check or unique-constraint commit race | No — the record exists |
| `PersistenceError` | DB connectivity, deadlock, other EF exception | Yes — with backoff |
| `Skipped` | Batch StopOnFirstError halted processing before this item | Yes — resubmit after fixing the earlier failure |

---

## 9. Future Transport Extensibility

The service owns: idempotency + mapping + hashing + result wrapping.  
The repository owns: the transport.

### Current (direct)

```
Service → IAuditEventRecordRepository → EfAuditEventRecordRepository → MySQL
```

### Queued (future)

```
Service → IAuditEventRecordRepository → QueuedAuditEventRecordRepository → RabbitMQ / Azure Service Bus
                                                             ↕
                                              AuditIngestWorker (consumer)
                                                             ↕
                                                         MySQL
```

Register `AddScoped<IAuditEventRecordRepository, QueuedAuditEventRecordRepository>()`.  
The idempotency probe becomes best-effort at produce time; the consumer enforces final idempotency at write via unique index.

### Outbox-driven (future)

```
Service → IAuditEventRecordRepository → OutboxAuditEventRecordRepository → AuditEventOutbox (same DB tx as source entity)
                                                             ↕
                                              AuditOutboxRelayWorker (background)
                                                             ↕
                                                   AuditEventRecords table
```

Provides transactional consistency: the audit record enters the outbox in the same DB transaction as the business entity mutation. The relay worker guarantees exactly-once delivery.

**No service interface or controller changes are required for either future transport.**

---

## 10. DI Registration

```csharp
// Program.cs — added below IAuditEventService (legacy)
builder.Services.AddScoped<IAuditEventIngestionService, AuditEventIngestionService>();
```

`IAuditEventService` / `AuditEventService` (legacy) is retained for backward compatibility — it still serves the old flat-DTO surface. It will be deprecated and removed when all controllers are migrated to the canonical service.

---

## 11. Build Verification

```
dotnet build --configuration Debug --verbosity quiet
Build succeeded.
    0 Warning(s)
    0 Error(s)
```
