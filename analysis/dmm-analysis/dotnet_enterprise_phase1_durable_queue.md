# Phase 1 — Durable Queue Design and Implementation

## Summary

Replaced the blocking in-memory scan queue with a **lease/ack abstraction** that supports
both in-memory (dev) and Redis Streams (production) without changing the worker or
orchestration code.

---

## Provider Selected

| Provider | When | Guarantee |
|----------|------|-----------|
| `InMemoryScanJobQueue` | `ScanWorker:QueueProvider = memory` | Volatile; jobs lost on restart |
| `RedisScanJobQueue` | `ScanWorker:QueueProvider = redis` | Durable; jobs survive restart |

---

## Architecture

### Interface (`IScanJobQueue`)

```
TryEnqueueAsync(job, ct)  → bool       non-blocking fail-fast enqueue
DequeueAsync(consumerId, ct) → ScanJobLease?  blocking dequeue with consumer identity
AcknowledgeAsync(lease, ct)              confirm successful processing
NackAsync(lease, ct)                     re-enqueue for retry (increments AttemptCount)
Count                                    approximate pending depth
```

The **lease pattern** decouples enqueue from commit:
- The lease holds both the `ScanJob` payload and the backend message ID
- Redis consumers hold a pending entry (PEL) until `AcknowledgeAsync` is called
- If the worker crashes before ACK, `XAUTOCLAIM` reclaims the job after `ClaimStaleJobsAfterSeconds`

### Redis Streams implementation

```
XADD  docs:scan:jobs * <fields>               → enqueue
XREADGROUP GROUP scan-workers worker-N > COUNT 1   → dequeue (new messages)
XAUTOCLAIM docs:scan:jobs scan-workers worker-N <idle-ms> 0-0 COUNT 10  → claim stale
XACK  docs:scan:jobs scan-workers <msgId>     → acknowledge
XDEL  docs:scan:jobs <msgId>                  → clean up stream
XLEN  docs:scan:jobs                          → queue depth
```

Consumer group creation uses `MKSTREAM` — the stream and group are created automatically on first boot.

### In-memory fallback

`BoundedChannelOptions.FullMode = DropWrite` — `TryWrite` returns `false` when the channel is full.
This enables the same fail-fast API without blocking the HTTP thread.

---

## Configuration

```json
{
  "ScanWorker": {
    "QueueProvider": "memory",
    "QueueCapacity": 1000,
    "StreamKey": "docs:scan:jobs",
    "ConsumerGroup": "scan-workers",
    "StreamMaxLength": 50000,
    "ClaimStaleJobsAfterSeconds": 300
  },
  "Redis": {
    "Url": "redis://localhost:6379"
  }
}
```

**Production**:
```json
{
  "ScanWorker": { "QueueProvider": "redis" },
  "Redis": { "Url": "redis://redis-primary:6379,redis-replica:6379,abortConnect=false" }
}
```

---

## Files Changed / Created

| File | Change |
|------|--------|
| `Documents.Domain/Entities/ScanJobLease.cs` | NEW — wraps ScanJob + MessageId |
| `Documents.Domain/Interfaces/IScanJobQueue.cs` | UPDATED — lease/ack interface |
| `Documents.Infrastructure/Scanner/ScanWorkerOptions.cs` | NEW — all scan worker config |
| `Documents.Infrastructure/Scanner/InMemoryScanJobQueue.cs` | UPDATED — DropWrite mode, metrics |
| `Documents.Infrastructure/Scanner/RedisScanJobQueue.cs` | NEW — XADD/XREADGROUP/XAUTOCLAIM |
| `Documents.Infrastructure/DependencyInjection.cs` | UPDATED — conditional queue wiring |
| `Documents.Api/appsettings.json` | UPDATED — full ScanWorker + Redis sections |

---

## Fallback Behavior

| Scenario | Result |
|----------|--------|
| Redis unavailable at startup | Process fails fast (config validation error) |
| Redis disconnects during XADD | `TryEnqueueAsync` returns `false` → 503 to client |
| Redis disconnects during XREADGROUP | Worker logs error, retries after 3s |
| Worker crashes mid-job (Redis) | Job stays in PEL; reclaimed after `ClaimStaleJobsAfterSeconds` |
| Worker crashes mid-job (memory) | Job is lost — acceptable for dev mode |

---

## Verification Steps

1. Start with `ScanWorker:QueueProvider=memory` — upload a document → `scanStatus: PENDING` in response
2. Worker processes it in background → check DB for `CLEAN` or `FAILED`
3. Switch to `redis` mode — upload document, kill process mid-scan → restart → job is reclaimed
4. Check `XLEN docs:scan:jobs` in Redis before and after to verify depth tracking
5. Verify `/metrics` exposes `docs_scan_queue_depth` and `docs_scan_jobs_enqueued_total`
