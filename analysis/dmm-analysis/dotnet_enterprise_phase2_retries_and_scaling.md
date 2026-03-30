# Phase 2 — Retry Strategy and Worker Scalability

## Summary

Added exponential backoff retry logic and configurable concurrent worker tasks to the
`DocumentScanWorker`. Multiple workers dequeue from the same queue safely (lease-based
dequeue prevents duplicate processing).

---

## Retry Policy

### Trigger conditions (transient failures)

| Error | Retry? |
|-------|--------|
| ClamAV TCP connection refused | ✅ Yes |
| ClamAV scan timeout | ✅ Yes |
| Storage download failure | ✅ Yes |
| `AttemptCount >= MaxRetryAttempts` | ❌ No — permanent `FAILED` |
| Malware detected (Infected) | ❌ No — expected outcome |

### Exponential backoff formula

```
delay_ms = min(InitialRetryDelaySeconds × 1000 × 2^attempt, MaxRetryDelaySeconds × 1000) + jitter(0–1000ms)
```

| Attempt | InitialDelay=5s, MaxDelay=120s |
|---------|-------------------------------|
| 0 → 1  | ~5s + jitter |
| 1 → 2  | ~10s + jitter |
| 2 → 3  | ~20s + jitter |
| 3+     | ≤120s + jitter |

### Retry flow

```
1. Download or scan fails
2. Worker checks: AttemptCount + 1 >= MaxRetryAttempts?
   YES → SetScanStatus(FAILED) + AuditScanFailed + AcknowledgeAsync
   NO  → AuditScanFailed(retrying=true) + Task.Delay(backoff) + NackAsync
3. NackAsync increments AttemptCount and re-enqueues the job
4. Worker picks up the new job and processes again
```

### Audit trail per retry

Each retry attempt emits a `SCAN_FAILED` audit event with:
```json
{
  "reason": "clamav_scan_error",
  "errorMessage": "Connection refused",
  "attempt": 2,
  "retrying": true,
  "delayMs": 10342
}
```

---

## Concurrency Model

### Worker count

`DocumentScanWorker.ExecuteAsync` spawns `WorkerCount` concurrent tasks via `Task.WhenAll`:

```csharp
var tasks = Enumerable.Range(0, WorkerCount)
    .Select(i => RunWorkerLoopAsync($"worker-{i}", stoppingToken))
    .ToArray();
await Task.WhenAll(tasks);
```

### Duplicate processing prevention

- **Redis Streams**: Each message is owned by one consumer (PEL). A second consumer cannot dequeue
  the same message while it is pending acknowledgment. `XAUTOCLAIM` only reclaims messages that have
  been idle for `ClaimStaleJobsAfterSeconds` — only crashed/stalled workers.
- **In-memory**: `Channel<T>` is thread-safe. `ReadAsync` is non-reentrant per message —
  each item is consumed exactly once.

### Recommended scaling tiers

| Load | Config |
|------|--------|
| Dev / Low | `WorkerCount=1`, `QueueProvider=memory` |
| Standard  | `WorkerCount=2`, `QueueProvider=redis` |
| High      | `WorkerCount=4`, `QueueProvider=redis`, multi-replica |
| Enterprise | Multiple pods with `WorkerCount=2-4` each, Redis cluster |

---

## Configuration

```json
{
  "ScanWorker": {
    "WorkerCount": 2,
    "MaxRetryAttempts": 3,
    "InitialRetryDelaySeconds": 5,
    "MaxRetryDelaySeconds": 120
  }
}
```

---

## Files Changed

| File | Change |
|------|--------|
| `Documents.Api/Background/DocumentScanWorker.cs` | REWRITTEN — concurrency + retry + lease/ack |
| `Documents.Infrastructure/Scanner/ScanWorkerOptions.cs` | NEW — WorkerCount, retry config |

---

## Verification Steps

1. Set `WorkerCount=2` and upload multiple documents simultaneously
2. Check logs for `[worker-0]` and `[worker-1]` messages processing concurrently
3. Stop ClamAV daemon and upload — worker should retry 3 times with increasing delay, then mark `FAILED`
4. Check audit trail: 3 `SCAN_FAILED` events with `retrying=true`, then 1 final `SCAN_FAILED` without
5. Restart ClamAV before `MaxRetryAttempts` — job should eventually complete `CLEAN`
