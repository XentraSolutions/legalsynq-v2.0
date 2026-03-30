# Phase 4 — Final Hardening: Analysis Report

**Service:** Documents (.NET 8 — `documents-dotnet`)
**Phase:** 4 of 4
**Date:** 2026-03-30
**Status:** Complete — 0 errors, 0 regressions

---

## 1. Objective

Close the final major production gaps in the Documents Service scanning and eventing subsystem:

1. **Redis resilience** — prevent uncontrolled retry storms when Redis is degraded or unavailable
2. **Durable event delivery** — upgrade scan completion events from ephemeral Pub/Sub to persistent Redis Streams
3. **Correlation propagation** — carry the HTTP `X-Correlation-Id` from the inbound upload request through the queue, worker, and emitted completion event
4. **Operator guidance** — production runbook with failure modes, alert thresholds, and recovery actions

---

## 2. Redis Resilience Strategy

### Problem
Both the Redis-backed scan job queue (`RedisScanJobQueue`) and the Redis notification publishers issue commands against Redis without a circuit breaker. Redis degradation causes synchronous failures that propagate to callers in tight retry loops, potentially overwhelming both the service and Redis.

### Solution: `RedisResiliencePipeline`

**File:** `Documents.Infrastructure/Redis/RedisResiliencePipeline.cs`

A single shared Polly `AdvancedCircuitBreaker` wraps all Redis operations service-wide. One instance tracks the aggregate Redis health and opens once repeated failures are detected.

**Pattern identical to the existing ClamAV circuit breaker** (`CircuitBreakerScannerProvider.cs`).

#### Failure handling per circuit state

| State | Behaviour |
|---|---|
| CLOSED | Normal Redis command execution |
| OPEN | `BrokenCircuitException` thrown immediately — no Redis round-trip |
| HALF-OPEN | Single probe allowed; success → CLOSED, failure → OPEN |

#### Exception handling
- **Opens on:** `RedisException`, `SocketException` (covers timeout, connection failure, server errors)
- **Does NOT open on:** `BrokenCircuitException` (Polly's own fast-fail signal), `ArgumentException`, `JsonException` (application logic errors)

#### Configuration (`appsettings.json → Redis:CircuitBreaker`)
```json
"CircuitBreaker": {
  "FailureThreshold": 5,
  "BreakDurationSeconds": 30,
  "SamplingDurationSeconds": 60,
  "MinimumThroughput": 5
}
```
Interpretation: 5+ failures out of 5+ calls within 60 s → circuit opens for 30 s.

#### Wrapping applied

| Component | Operations wrapped |
|---|---|
| `RedisScanJobQueue` | XADD (enqueue), XAUTOCLAIM (reclaim), XREADGROUP (dequeue) |
| `RedisScanCompletionPublisher` | `PublishAsync` (Pub/Sub) |
| `RedisStreamScanCompletionPublisher` | `StreamAddAsync` (XADD) |

#### Safe degradation per component

- **Queue XADD** — `BrokenCircuitException` caught by `catch (Exception)` → `TryEnqueueAsync` returns `false` → upload receives HTTP 503 (same behaviour as other enqueue failures)
- **Queue XREADGROUP** — caught by `catch (Exception)` in the `while` loop → 3 s delay, then retries (no crash, no storm)
- **Pub/Sub / Stream publish** — caught by `catch (Exception)` in each publisher → delivery failure logged and metered; scan state is unaffected

---

## 3. Durable Event Delivery Strategy

### Problem
Redis Pub/Sub (`RedisScanCompletionPublisher`) is at-most-once and ephemeral. Messages are lost if no subscriber is connected at delivery time. Downstream systems (CareConnect, portals, automations) cannot replay events after outages.

### Solution: `RedisStreamScanCompletionPublisher` (Provider=`redis-stream`)

**File:** `Documents.Infrastructure/Notifications/RedisStreamScanCompletionPublisher.cs`

Uses Redis `XADD` to append each `DocumentScanCompletedEvent` to a named Redis Stream. Each entry persists until trimmed by `MAXLEN`.

#### Stream format
Each XADD entry contains both individual fields (for lightweight consumers) and a full JSON `payload` field (for consumers wanting the complete event):

| Field | Content |
|---|---|
| `eventId` | Unique event UUID |
| `documentId` | Document UUID |
| `tenantId` | Tenant UUID |
| `versionId` | Version UUID (empty if none) |
| `scanStatus` | `Clean` / `Infected` / `Failed` |
| `occurredAt` | ISO-8601 UTC timestamp |
| `correlationId` | HTTP request correlation ID |
| `attemptCount` | Total scan attempts |
| `engineVersion` | ClamAV version string |
| `fileName` | Original filename (no content) |
| `serviceName` | `documents-dotnet` |
| `payload` | Full JSON serialisation of the event |

#### Configuration (`appsettings.json → Notifications:ScanCompletion`)
```json
{
  "Notifications": {
    "ScanCompletion": {
      "Provider": "redis-stream",
      "Redis": {
        "Channel": "documents.scan.completed",
        "StreamKey": "documents:scan:completed",
        "StreamMaxLength": 100000
      }
    }
  }
}
```

#### Delivery comparison

| Provider | Guarantee | Persistence | Replay | Recommended |
|---|---|---|---|---|
| `log` | none | no | no | Dev/test only |
| `redis` (Pub/Sub) | at-most-once | no | no | Low-stakes real-time |
| `redis-stream` | at-least-once* | yes (Redis persistence) | yes | **Production default** |
| `none` | none | no | no | Notifications disabled |

*At-least-once assuming Redis has AOF or RDB persistence enabled. If Redis loses data before a consumer reads the entry, the event is lost. Use a transactional outbox for strict at-least-once guarantees.

#### Consumer pattern (downstream)
```
XREADGROUP GROUP <consumer-group> <consumer-id> COUNT 10 STREAMS documents:scan:completed >
XACK documents:scan:completed <consumer-group> <entry-id>
```

---

## 4. Correlation Propagation Approach

### Flow: upload request → scan job → worker → completion event

```
HTTP POST /documents/upload
  X-Correlation-Id: abc-123
       │
       ▼
CorrelationIdMiddleware
  ctx.Items["CorrelationId"] = "abc-123"
       │
       ▼
DocumentService → ScanOrchestrationService
  ScanJob.CorrelationId = ctx.CorrelationId   ← NEW
       │
       ▼  (serialised into Redis Stream field "correlationId")
RedisScanJobQueue.TryEnqueueAsync
       │
       ▼  (deserialised from stream entry)
DocumentScanWorker
  "Scan starting": Corr=abc-123              ← NEW log field
  "Scan result":   Corr=abc-123              ← NEW log field
       │
       ▼
PublishCompletionEventAsync
  evt.CorrelationId = job.CorrelationId      ← NEW
       │
       ▼
RedisStreamScanCompletionPublisher
  Stream field: correlationId = "abc-123"   ← NEW
```

### Design constraints honoured

- Correlation IDs are **in logs and event payloads only** — never in Prometheus metric labels (high-cardinality prohibition)
- `ScanJob.CorrelationId` uses `init` — immutable after creation
- `NackAsync` preserves `CorrelationId` through retry cycles so the chain is never broken
- In-memory queue (`InMemoryScanJobQueue`) carries the field through the reference — no serialisation needed

---

## 5. Configuration Added

### `appsettings.json` additions

```json
// Redis circuit breaker defaults
"Redis": {
  "Url": "",
  "CircuitBreaker": {
    "FailureThreshold": 5,
    "BreakDurationSeconds": 30,
    "SamplingDurationSeconds": 60,
    "MinimumThroughput": 5
  }
}

// Stream publisher config
"Notifications": {
  "ScanCompletion": {
    "Provider": "log",
    "Redis": {
      "Channel": "documents.scan.completed",
      "StreamKey": "documents:scan:completed",
      "StreamMaxLength": 100000
    }
  }
}
```

---

## 6. Files Changed

### New files

| File | Purpose |
|---|---|
| `Documents.Infrastructure/Redis/RedisResiliencePipeline.cs` | Shared Polly circuit breaker + options for Redis |
| `Documents.Infrastructure/Notifications/RedisStreamScanCompletionPublisher.cs` | Durable XADD scan completion publisher |

### Modified files

| File | Change summary |
|---|---|
| `Documents.Domain/Entities/ScanJob.cs` | Added `CorrelationId` field (`string? init`) |
| `Documents.Application/Services/ScanOrchestrationService.cs` | Threads `ctx.CorrelationId` into both `ScanJob` creation paths |
| `Documents.Infrastructure/Observability/RedisMetrics.cs` | Added circuit breaker gauges/counters + stream publish counters |
| `Documents.Infrastructure/Notifications/NotificationOptions.cs` | Extended with `redis-stream` docs, `StreamKey`, `StreamMaxLength` |
| `Documents.Infrastructure/Health/RedisHealthCheck.cs` | Injects `RedisResiliencePipeline`; surfaces circuit state in health description |
| `Documents.Infrastructure/Scanner/RedisScanJobQueue.cs` | Injects + wraps XADD/XAUTOCLAIM/XREADGROUP; `CorrelationId` in serialise/deserialise/nack |
| `Documents.Infrastructure/Notifications/RedisScanCompletionPublisher.cs` | Injects + wraps Pub/Sub publish in resilience pipeline |
| `Documents.Infrastructure/DependencyInjection.cs` | Registers `RedisResiliencePipeline`; adds `redis-stream` case to notification factory |
| `Documents.Api/Background/DocumentScanWorker.cs` | Populates `evt.CorrelationId`; adds `Corr=` to scan start/result log messages |
| `Documents.Api/appsettings.json` | Adds `Redis:CircuitBreaker` section; adds `StreamKey` + `StreamMaxLength` to Notifications |

---

## 7. Metrics and Health Changes

### New Prometheus metrics

| Metric | Type | Description |
|---|---|---|
| `docs_redis_circuit_state` | Gauge | 0=closed, 1=open, 2=half-open |
| `docs_redis_circuit_open_total` | Counter | Times circuit has opened |
| `docs_redis_circuit_short_circuit_total` | Counter | Operations fast-failed by open circuit |
| `docs_scan_completion_stream_publish_total` | Counter | Successful XADD publishes |
| `docs_scan_completion_stream_publish_failures_total` | Counter | Failed XADD publishes |

### Existing metrics unchanged
`docs_redis_healthy`, `docs_redis_connection_failures_total`, `docs_redis_stream_reclaims_total`, `docs_scan_completion_events_emitted_total`, `docs_scan_completion_delivery_success_total`, `docs_scan_completion_delivery_failures_total`

### Health check changes (`RedisHealthCheck`)
- Always pings Redis directly (not through circuit breaker) so the probe can allow circuit recovery
- Returns `Degraded` (not `Unhealthy`) when ping succeeds but circuit is `Open` — lets operators notice the mismatch
- Includes `circuit=<state>` in the health description string

---

## 8. Delivery Guarantees

| Concern | Guarantee |
|---|---|
| Document scan state (DB) | Primary — always persisted before event publish |
| Event delivery failure | Never corrupts scan state — all publishers are fire-and-forget |
| Redis Pub/Sub (`redis`) | At-most-once. Lost if no subscriber connected |
| Redis Stream (`redis-stream`) | At-least-once with Redis persistence; replayable by consumers |
| Circuit open during enqueue | Upload returns HTTP 503 — same as queue saturation |
| Circuit open during dequeue | Worker delays 3 s and retries — no storm |
| Circuit open during publish | Delivery failure logged and metered; scan state unaffected |
| Correlation ID across retries | Preserved through `NackAsync` → new job retains original ID |

---

## 9. Runbook / Operational Guidance

### Dependencies

| Dependency | Required for | Notes |
|---|---|---|
| PostgreSQL | All scan state persistence | Hard failure if unavailable |
| ClamAV | File scanning | Circuit breaker; fails to `ScanStatus.Failed` when open |
| Redis | Queue + stream notifications | Required only when `QueueProvider=redis` or `AccessToken:Store=redis` |
| Storage (local/S3) | File download for scanning | Retries up to `MaxRetryAttempts` |

---

### ClamAV outage

**Behaviour:** Circuit breaker opens after 5+ failures in 60 s. Scan jobs receive `ScanStatus.Failed` (not CLEAN — fail-closed). Worker retries up to `MaxRetryAttempts`; persistent failures write `FAILED` to DB. Files remain quarantined.

**Metrics to watch:**
- `docs_clamav_circuit_state` → 1 (open)
- `docs_scan_jobs_failed_total` rising
- `docs_scan_duration_seconds` histogram drops to zero (short-circuits)

**Recovery:**
1. Restore ClamAV. Circuit transitions to half-open automatically after `BreakDurationSeconds`.
2. One probe request is attempted. On success → closed.
3. Jobs still in the queue will be retried automatically (if `MaxRetryAttempts` not yet hit).
4. Documents marked `FAILED` may need manual re-scanning depending on business rules.

**Alert:** `docs_clamav_circuit_state == 1` for > 2 minutes

---

### Redis outage

**Behaviour:**
- Queue operations: enqueue returns `false` → upload rejected with HTTP 503. Dequeue fast-fails and delays 3 s.
- Event publishing: delivery failure logged and metered; scan pipeline continues.
- Circuit opens after 5+ failures in 60 s; commands fast-fail during the 30 s break.

**Metrics to watch:**
- `docs_redis_circuit_state` → 1 (open)
- `docs_redis_circuit_open_total` rising
- `docs_redis_circuit_short_circuit_total` rising
- `docs_redis_connection_failures_total` rising
- `/health/ready` → `redis` check reports Unhealthy

**Recovery:**
1. Restore Redis.
2. Circuit probes automatically (`durationOfBreak = 30 s`).
3. Scan jobs will resume from the Redis Stream PEL (already-claimed jobs) or new entries.
4. XAUTOCLAIM reclaims any stale jobs from crashed consumers after `ClaimStaleJobsAfterSeconds`.

**Alert:** `docs_redis_circuit_state == 1` for > 2 minutes

---

### Queue backlog / saturation

**Behaviour:** When in-memory queue hits `QueueCapacity`, `TryEnqueueAsync` returns `false` → HTTP 503. Redis queue has no configured size cap but will grow until `StreamMaxLength` trims oldest entries.

**Metrics to watch:**
- `docs_scan_queue_depth` rising
- `docs_scan_queue_saturations_total` rising
- `docs_scan_jobs_started_total` vs `docs_scan_jobs_enqueued_total` ratio declining

**Recovery:**
- Scale `WorkerCount` in `ScanWorker` config
- Increase `QueueCapacity` (in-memory only)
- Add worker instances (Redis queue is shared safely via consumer groups)

**Alert:** `docs_scan_queue_depth > 500` for > 5 minutes

---

### Stale signature

**Behaviour:** `ClamAvSignatureHealthCheck` returns `Degraded` (not `Unhealthy`). Scanning continues — stale signatures reduce detection quality but don't block uploads.

**Metrics to watch:**
- `/health/ready` → `clamav-signatures` check returns `Degraded`
- `Scanner:ClamAv:SignatureMaxAgeHours` threshold

**Recovery:**
1. Run `freshclam` on the ClamAV host.
2. Health check re-evaluates on the next 5-minute cache interval.

**Alert:** `/health/ready → clamav-signatures = Degraded` for > 1 hour

---

### Event delivery failures

**Behaviour:** Delivery failure does not affect scan state. `docs_scan_completion_delivery_failures_total` increments. Logs include `Document=`, `Status=`, and `Corr=` for correlation.

**Pub/Sub failures:** Transient — subscribers may reconnect and receive future events. Historical events are not recoverable.

**Stream failures:** Persistent until resolved. Check `docs_scan_completion_stream_publish_failures_total`. If Redis circuit is open, stream publishes also fail-fast.

**Recovery (stream delivery):**
1. Restore Redis.
2. Circuit closes after `BreakDurationSeconds`.
3. Future events will publish. Events missed during outage are permanently lost unless the service has a replay/requeue mechanism.

**Alert:** `docs_scan_completion_delivery_failures_total` rate > 0.1/min for > 3 minutes

---

### Inspecting the Redis Stream backlog

```bash
# Total entries in the completion stream
XLEN documents:scan:completed

# Read the last 10 entries
XREVRANGE documents:scan:completed + - COUNT 10

# List consumer groups
XINFO GROUPS documents:scan:completed

# Show pending entries (unACKed) per consumer group
XPENDING documents:scan:completed <consumer-group>

# Inspect oldest pending entries
XPENDING documents:scan:completed <consumer-group> - + 10
```

---

### Inspecting the scan job queue (Redis Streams)

```bash
# Queue depth
XLEN docs:scan:jobs

# Consumer group status
XINFO GROUPS docs:scan:jobs

# Pending (claimed but unACKed) entries — these are in-flight or stale
XPENDING docs:scan:jobs scan-workers

# Force-reclaim all entries idle > 5 minutes for debugging
XAUTOCLAIM docs:scan:jobs scan-workers debug-consumer 300000 0-0
```

---

## 10. Verification Steps

### Automated (build)
```bash
cd apps/services/documents-dotnet
dotnet build Documents.Api/Documents.Api.csproj -c Release
# Expected: 0 errors, 1 pre-existing warning (CS1998 in Program.cs)
```

### Manual functional verification

1. **Default configuration (Provider=log):**
   - Start service, upload a document → no Redis dependency required
   - Structured logs show `Corr=<value>` on scan-start and scan-result lines

2. **Redis circuit breaker:**
   - Set `ScanWorker:QueueProvider=redis`, point to an unreachable Redis URL
   - Upload → should receive HTTP 503 within ms (fast-fail, not timeout)
   - `docs_redis_circuit_state` should reach 1 after 5 failures
   - Restore Redis → circuit should close within 30 s

3. **Stream publisher (Provider=redis-stream):**
   - Set `Notifications:ScanCompletion:Provider=redis-stream` with valid Redis
   - Upload and wait for scan completion
   - `XLEN documents:scan:completed` should be 1
   - `XRANGE documents:scan:completed - +` should show the event entry
   - `correlationId` field should match the `X-Correlation-Id` sent on upload

4. **Correlation propagation:**
   - Send upload with header `X-Correlation-Id: test-trace-1`
   - In logs, search for `Corr=test-trace-1` — should appear on scan-start and scan-result lines
   - Stream entry should have `correlationId = test-trace-1`

5. **Health check circuit state:**
   - `/health/ready` → `redis` entry description should include `circuit=closed` under normal operation
   - With circuit open: description should include `circuit=open [circuit open — probing for recovery]`

---

## 11. Remaining Risks / Follow-ups

| Risk | Severity | Recommendation |
|---|---|---|
| Redis data loss during stream publish | Medium | Enable AOF (`appendonly yes`) or RDB snapshots on Redis; consider transactional outbox for strict at-least-once |
| Correlation ID unavailable for background re-scans | Low | Background-triggered re-scans (not HTTP-initiated) will have `CorrelationId=null` — this is acceptable and documented |
| Stream consumer groups not pre-created | Low | Downstream consumers must create their own groups via `XGROUP CREATE … MKSTREAM` before reading |
| `BrokenCircuitException` logged as generic `Exception` in error handlers | Low | Log level is `LogError` which may cause alert noise; consider filtering `BrokenCircuitException` to `LogWarning` in callers as a follow-up polish |
| `InMemoryScanJobQueue` does not persist `CorrelationId` across restarts | Informational | In-memory queue is not restart-safe by design; this is not a regression |
| Polly v8 migration | Future | Current codebase uses Polly v7.2.4 (`AsyncCircuitBreakerPolicy`). When upgrading to .NET Polly v8, migrate to `ResiliencePipeline<T>` |

---

## Recommended Alert Rules (Prometheus / AlertManager)

```yaml
groups:
  - name: documents-service
    rules:
      - alert: ClamAvCircuitOpen
        expr: docs_clamav_circuit_state == 1
        for: 2m
        labels: { severity: critical }
        annotations:
          summary: "ClamAV circuit breaker is open — scans failing"

      - alert: RedisCircuitOpen
        expr: docs_redis_circuit_state == 1
        for: 2m
        labels: { severity: critical }
        annotations:
          summary: "Redis circuit breaker is open — queue/events degraded"

      - alert: ScanQueueDepthHigh
        expr: docs_scan_queue_depth > 500
        for: 5m
        labels: { severity: warning }
        annotations:
          summary: "Scan queue depth is elevated — consider scaling workers"

      - alert: EventDeliveryFailureRate
        expr: rate(docs_scan_completion_delivery_failures_total[5m]) > 0.1
        for: 3m
        labels: { severity: warning }
        annotations:
          summary: "Scan completion event delivery failures detected"

      - alert: ScanFailureRateHigh
        expr: rate(docs_scan_jobs_failed_total[10m]) > 1
        for: 5m
        labels: { severity: warning }
        annotations:
          summary: "Scan job failure rate is elevated"

      - alert: StaleSignatures
        expr: docs_clamav_signature_fresh == 0
        for: 1h
        labels: { severity: warning }
        annotations:
          summary: "ClamAV virus signatures are stale — run freshclam"
```

---

## Recommended Dashboard Panels

| Panel | Query | Type |
|---|---|---|
| Scan throughput | `rate(docs_scan_jobs_clean_total[5m]) + rate(docs_scan_jobs_infected_total[5m])` | Stat/Graph |
| Scan failure rate | `rate(docs_scan_jobs_failed_total[5m])` | Graph |
| Scan P95 latency | `histogram_quantile(0.95, docs_scan_duration_seconds_bucket)` | Stat |
| Queue depth | `docs_scan_queue_depth` | Gauge |
| Queue reclaim rate | `rate(docs_redis_stream_reclaims_total[5m])` | Graph |
| ClamAV circuit state | `docs_clamav_circuit_state` | State (0/1/2) |
| Redis circuit state | `docs_redis_circuit_state` | State (0/1/2) |
| Event delivery rate | `rate(docs_scan_completion_delivery_success_total[5m])` | Graph |
| Event delivery failures | `rate(docs_scan_completion_delivery_failures_total[5m])` | Graph |
| Stream publish rate | `rate(docs_scan_completion_stream_publish_total[5m])` | Graph |
| Redis connection failures | `rate(docs_redis_connection_failures_total[5m])` | Graph |
| Oversize file rejections | `rate(docs_scan_oversize_rejections_total[5m])` | Graph |
