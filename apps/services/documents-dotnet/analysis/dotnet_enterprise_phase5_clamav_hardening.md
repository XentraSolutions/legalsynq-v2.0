# Phase 5 — ClamAV Hardening and Production Review

## Summary

Hardened the ClamAV integration with a dedicated health check, proper timeout handling,
TCP connection isolation per-scan, and fail-closed security posture review.

---

## ClamAV Health Check

### Implementation

`ClamAvHealthCheck` (IHealthCheck) sends the `zPING\0` command to clamd and checks for `PONG`:

```csharp
var ping = Encoding.ASCII.GetBytes("zPING\0");
await stream.WriteAsync(ping, cts.Token);
var response = (await reader.ReadLineAsync(cts.Token))?.Trim();
return response == "PONG" ? HealthCheckResult.Healthy(...) : HealthCheckResult.Degraded(...)
```

Timeout is capped at `min(ClamAv:TimeoutMs, 5000ms)` to avoid blocking the health check
for longer than the readiness probe allows.

Updates `docs_clamav_healthy` gauge (1=up, 0=down) after every check.

### Registration

```csharp
services.AddHealthChecks()
    .AddCheck<ClamAvHealthCheck>("clamav", failureStatus: HealthStatus.Degraded, tags: ["ready"]);
```

ClamAV is `Degraded` (not `Unhealthy`) so the liveness probe (`/health`) continues
to return `200` even when clamd is down. Only the readiness probe (`/health/ready`) reflects
the degraded state. The worker will retry scan jobs until ClamAV recovers.

---

## Timeout Handling

### Scan timeout

`ClamAvOptions.TimeoutMs` (default: 30,000ms) is set as a `Socket.ReceiveTimeout` and
`Socket.SendTimeout` via the `TcpClient`. If clamd hangs:
- `ScanAsync` throws a `SocketException` (timeout)
- Worker catches it as a transient error → retry with backoff
- Logged at `Warning` level, not `Error`

### Health check timeout

Health check uses a separate `CancellationTokenSource` capped at 5 seconds to prevent
blocking the Kubernetes probe for more than the `timeoutSeconds` limit.

---

## Connection Isolation

Each scan creates a **new `TcpClient`** connection to clamd. Connections are not shared
or pooled. This prevents correlated failures where one stuck connection blocks all scans.

The `TcpClient` is wrapped in `using` blocks and disposed after each scan or health check.

---

## Fail-Closed Posture

| Scenario | Behavior |
|----------|----------|
| ClamAV unavailable at upload | Upload succeeds (async model); scan fails with retry |
| ClamAV unavailable at scan time | Worker retries with backoff; document stays `PENDING` |
| Scan times out | Treated as transient failure; retry |
| Scan returns `FOUND` (malware) | Document permanently `INFECTED`; file purged from storage |
| Scan returns `ERROR` | Treated as `FAILED`; retry |
| `RequireCleanScanForAccess=true` | Access denied while `PENDING` or `FAILED` |
| `RequireCleanScanForAccess=false` | Access allowed for `PENDING`/`FAILED` (dev mode) |
| Scanner bypassed (`Provider=none`) | All files pass as "not scanned"; access not blocked |

**No scenario marks a file as CLEAN without a successful scan response from ClamAV.**

---

## Resilience Patterns Applied

| Pattern | Implementation |
|---------|----------------|
| Retry | `DocumentScanWorker` retry loop with exponential backoff |
| Fail-closed | `RequireCleanScanForAccess=true` default |
| Health check | `ClamAvHealthCheck` on `/health/ready` |
| Connection isolation | New `TcpClient` per scan |
| Timeout | `TimeoutMs` on socket + health check cap |
| Graceful degradation | ClamAV `Degraded` doesn't fail liveness |
| Audit trail | Every retry, failure, and access denial audited |

**Note**: A circuit-breaker pattern (e.g. Polly) is NOT implemented in this phase.
For high-throughput environments with frequent ClamAV availability issues, consider
adding a circuit breaker with `Polly.Extensions.Http` or similar.

---

## Production Configuration Reference

```json
{
  "Scanner": {
    "Provider": "clamav",
    "ClamAv": {
      "Host": "clamd-service",
      "Port": 3310,
      "TimeoutMs": 30000,
      "ChunkSizeBytes": 2097152
    }
  },
  "ScanWorker": {
    "QueueProvider": "redis",
    "WorkerCount": 2,
    "MaxRetryAttempts": 3,
    "InitialRetryDelaySeconds": 5,
    "MaxRetryDelaySeconds": 120,
    "ClaimStaleJobsAfterSeconds": 300
  },
  "Documents": {
    "RequireCleanScanForAccess": true
  }
}
```

---

## Remaining Risks

| Risk | Severity | Mitigation |
|------|----------|-----------|
| No circuit breaker — clamd unavailability floods retry queue | Medium | Add Polly circuit breaker (next backlog item) |
| ClamAV signature database staleness | Medium | Ensure `freshclam` daemon runs on clamav host; health check does not verify DB age |
| Large file memory pressure (INSTREAM 26MB default limit) | Medium | `ChunkSizeBytes` controls chunking; clamd default `StreamMaxLength=25MB` — validate |
| Single Redis write point | Low (clustered) | Use Redis Sentinel or Cluster for HA |
| Worker count too high saturates ClamAV | Medium | Monitor `docs_scan_duration_seconds`; scale ClamAV horizontally if needed |

---

## Files Changed

| File | Change |
|------|--------|
| `Documents.Infrastructure/Health/ClamAvHealthCheck.cs` | NEW — PING/PONG health check |
| `Documents.Infrastructure/Scanner/ClamAvFileScannerProvider.cs` | Timeout isolation (existing) |
| `Documents.Api/Endpoints/HealthEndpoints.cs` | UPDATED — ASP.NET Core MapHealthChecks |

---

## Verification Steps

1. Start with ClamAV running: `GET /health/ready` → both checks `healthy`
2. Stop ClamAV: `GET /health/ready` → `clamav: degraded`, `GET /health` → still `healthy`
3. Upload a file with ClamAV down → `scanStatus: PENDING` immediately (worker retries)
4. Restart ClamAV before max retries → job completes `CLEAN`
5. Check `docs_clamav_healthy` at `/metrics` → `0` when down, `1` when up
6. Upload an EICAR test file → status becomes `INFECTED`, access denied with `SCAN_BLOCKED`
