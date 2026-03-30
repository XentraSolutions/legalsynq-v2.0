# Step 10 — Tamper-Evident Hashing: Analysis Report

Platform Audit/Event Service  
Date: 2026-03-30

---

## Objective

Enhance the ingestion flow with deterministic, tamper-evident hashing so that every persisted `AuditEventRecord` carries a cryptographic hash that covers its own canonical fields **plus** the hash of the immediately preceding record in the same chain. This forms a singly-linked hash chain: modifying any historical record invalidates all subsequent hashes without requiring a global ledger.

---

## Hash Algorithm

### Primary: HMAC-SHA256 (production)

- **Class:** `System.Security.Cryptography.HMACSHA256`
- **Output:** 64-character lowercase hexadecimal string (256-bit digest).
- **Key:** 256-bit (32-byte) secret loaded from `Integrity:HmacKeyBase64` (Base64-encoded).
- **Properties:** Integrity + Authentication — without the secret, an attacker with write access to the record store cannot forge a valid hash.
- **Activation:** When `Integrity:Algorithm = "HMAC-SHA256"` and `HmacKeyBase64` is non-empty.
- **Development fallback:** When `HmacKeyBase64` is absent in HMAC-SHA256 mode, signing is silently skipped (records receive `Hash = null`). A startup warning is logged.

### Secondary: SHA-256 (portable / development)

- **Class:** `System.Security.Cryptography.SHA256.HashData()`
- **Output:** 64-character lowercase hexadecimal string (256-bit digest).
- **Key:** None required.
- **Properties:** Integrity only — an attacker with write access can replace a record and recompute a matching hash. Not suitable for adversarial production environments.
- **Activation:** When `Integrity:Algorithm = "SHA-256"`. Always active; no secrets management needed.
- **Recommendation:** Use in development, CI, integration tests, and air-gapped deployments.

---

## Fields Included in Hash Payload

The canonical payload is assembled by `AuditRecordHasher.BuildPayload()` as a **pipe-delimited string**. Field order is fixed and must not change — any change breaks all stored hashes.

```
{AuditId}|{EventType}|{SourceSystem}|{TenantId}|{ActorId}|{EntityType}|{EntityId}|{Action}|{OccurredAtUtc}|{RecordedAtUtc}|{PreviousHash}
```

| Pos | Field           | Format | Nullable treatment |
|-----|-----------------|--------|--------------------|
| 0   | `AuditId`       | `Guid("D")` — lowercase + hyphens | Required |
| 1   | `EventType`     | Verbatim string | Required |
| 2   | `SourceSystem`  | Verbatim string | Required |
| 3   | `TenantId`      | Verbatim string | `""` when null |
| 4   | `ActorId`       | Verbatim string | `""` when null |
| 5   | `EntityType`    | Verbatim string | `""` when null |
| 6   | `EntityId`      | Verbatim string | `""` when null |
| 7   | `Action`        | Verbatim string | Required |
| 8   | `OccurredAtUtc` | `DateTimeOffset("O")` — ISO 8601 | Required |
| 9   | `RecordedAtUtc` | `DateTimeOffset("O")` — ISO 8601 | Required |
| 10  | `PreviousHash`  | Verbatim hex string | `""` when null (genesis) |

### Fields intentionally excluded from the payload

The following fields are excluded by design:

| Field | Reason for exclusion |
|-------|---------------------|
| `Description` | May be redacted for PII compliance without invalidating chain integrity. |
| `BeforeJson` / `AfterJson` | Same PII redaction rationale. |
| `MetadataJson` / `TagsJson` | Unstructured; may evolve post-ingest for annotation purposes. |
| `CorrelationId` / `RequestId` / `SessionId` | Tracing fields; set by infrastructure, not by the event source. |
| `ActorName` / `ActorIpAddress` / `ActorUserAgent` | PII — name/IP may need redaction. |
| `SourceService` / `SourceEnvironment` | Routing metadata, not part of the logical event identity. |
| `Id` (surrogate key) | Database-assigned; not known before insert. |
| `IsReplay` | Replay status is a processing annotation, not part of the event identity. |

---

## Chain Logic

### Chain scope

The chain is scoped to `(TenantId, SourceSystem)`. This prevents cross-tenant entanglement and allows independent chain traversal per source system within a tenant.

### Chain head lookup

`GetLatestInChainAsync(tenantId, sourceSystem)` returns the record with the highest `Id` (auto-increment surrogate → insertion order) in the scope. This record's `Hash` becomes `PreviousHash` for the new record.

### Genesis record

When no prior record exists, `PreviousHash = null`. The payload uses `""` for position 10, and the record still receives a `Hash`. The chain can be verified from the genesis record forward.

### PreviousHash in the payload (key enhancement)

**Prior state (pre-Step 10):** `PreviousHash` was stored on the record (as a linked-list pointer) but was NOT included in the canonical hash payload. Hash(N) did not depend on Hash(N-1).

**After Step 10:** `PreviousHash` is the last field in `BuildPayload()`. Hash(N) now cryptographically depends on Hash(N-1):

```
H1 = hash("...|")          genesis — empty PreviousHash
H2 = hash("...|H1")        depends on H1
H3 = hash("...|H2")        depends on H2 (and transitively on H1)
```

Modifying any field of record N changes H_N, which then invalidates H_{N+1}, ..., H_{last}. A verifier scanning the chain detects the first hash mismatch and can identify the tampered record precisely.

### No mutation after insert

`AuditEventRecord.Hash` and `AuditEventRecord.PreviousHash` are both `init`-only properties. The `AppendAsync` contract is insert-only. The ingest service computes both values before calling the mapper, so the entity is fully populated at construction time.

---

## Implementation: Separation of Concerns

### `AuditRecordHasher` (static utility class)

Separated into two public stages:

```csharp
// Stage 1 — Payload builder (pure, deterministic, testable)
string BuildPayload(Guid auditId, string eventType, ..., string? previousHash)
string BuildPayload(AuditEventRecord record)

// Stage 2 — Hash functions (algorithm-specific, side-effect-free)
string ComputeSha256(string payload)       // keyless
string ComputeHmacSha256(string payload, byte[] hmacSecret)

// Verification
bool Verify(AuditEventRecord record, string algorithm, byte[]? hmacSecret = null)
```

`BuildPayload()` is public so:
- Tests can inspect the exact canonical string without black-box guessing.
- Diagnostic tools can reproduce the hash independently.
- Documentation can be validated programmatically.

### `AuditEventIngestionService` (orchestrator)

Steps 3 and 4 in `IngestOneAsync`:

```
Step 3: previousHash = GetLatestInChainAsync(tenantId, sourceSystem)?.Hash
Step 4: payload      = BuildPayload(..., previousHash)
        hash         = algorithm == SHA-256
                       ? ComputeSha256(payload)
                       : ComputeHmacSha256(payload, hmacSecret)
```

The service does NOT compute the hash inside the mapper (which is side-effect-free) — it computes externally and passes both `hash` and `previousHash` as parameters to `ToEntity()`.

---

## Limitations and Future Enhancements

### Current limitations

| # | Limitation | Impact | Mitigation |
|---|-----------|--------|------------|
| 1 | **Key rotation not supported** | Rotating the HMAC key invalidates all existing hashes. | Planned: `HashAlgorithmVersion` column + multi-key verification window. |
| 2 | **Batch ingest chain fork** | Two concurrent batches for the same scope may read the same chain head, creating a fork (two records with identical `PreviousHash`). | The fork is detectable. Future: pessimistic table-scope lock or compare-and-swap on chain head. |
| 3 | **Non-hashed fields** | `Description`, JSON blobs, PII fields, and tracing fields are outside the canonical set. Post-insert modification is not detectable via hash. | Intentional: PII redaction must not break chain integrity. Separate redaction-log pattern planned. |
| 4 | **In-memory EF ordering** | `GetLatestInChainAsync` depends on auto-increment `Id` ordering. In-memory EF provider may not assign IDs in insertion order. | Use MySQL provider in integration tests when chain ordering is tested. |
| 5 | **SHA-256 forgery risk** | SHA-256 mode does not resist a write-privileged attacker who can recompute hashes. | Documented clearly. HMAC-SHA256 required for adversarial production environments. |

### Planned future enhancements

| Enhancement | Description |
|-------------|-------------|
| **UUIDv7 AuditId** | Replace `Guid.NewGuid()` (random) with a time-ordered UUIDv7 factory. Improves clustered-index insert locality on MySQL and makes `AuditId` lexicographically sortable. |
| **HashAlgorithmVersion column** | A `HashAlgorithmVersion` byte on `AuditEventRecord` records which algorithm+key combination produced `Hash`. Enables seamless key rotation and algorithm agility without re-hashing. |
| **Integrity checkpoint records** | Periodic `IntegrityCheckpoint` records (already modelled) that capture a snapshot hash of the chain tip. Enables O(1) chain-tip verification without scanning the full history. |
| **Global chain verification endpoint** | `GET /api/integrity/verify?tenantId=&sourceSystem=` streams the chain and returns the first gap or mismatch. Uses `StreamForExportAsync` for memory-efficient traversal. |
| **Redaction log** | A `PiiRedactionLog` table records when and what fields were redacted from a record, signed with a separate key. Allows PII removal without breaking the main chain. |
| **Chain lock** | A `SELECT ... FOR UPDATE` on the chain head row during ingest prevents the concurrent-fork race window. Requires switching from InMemory to MySQL for the lock to be effective. |

---

## Build Status

- `PlatformAuditEventService`: ✅ 0 errors, 0 warnings (verified after Step 10)

---

## Files Changed

| File | Change |
|------|--------|
| `Utilities/AuditRecordHasher.cs` | Full rewrite: `BuildPayload()` public + `previousHash` in canonical set; `ComputeSha256()` and `ComputeHmacSha256()` public; `Verify()` algorithm-aware; removed old `Compute()` overload. |
| `Services/AuditEventIngestionService.cs` | Added `_algorithm` and `_signingEnabled` fields; constructor logs signing state; Steps 3–4 use `_signingEnabled` guard; Step 4 calls `BuildPayload(previousHash)` then dispatches to algorithm. |
| `Configuration/IntegrityOptions.cs` | Documented `"SHA-256"` as a supported `Algorithm` value. |
| `appsettings.Development.json` | Added explicit `Algorithm: HMAC-SHA256` for clarity. |
| `Docs/integrity-model.md` | New: integrity model specification for operators and auditors. |
| `analysis/step10_hashing.md` | New: this analysis report. |
